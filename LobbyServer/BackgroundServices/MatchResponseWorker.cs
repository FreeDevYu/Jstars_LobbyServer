using LobbyAPI.Models;
using LobbyServer.Hubs; 
using Microsoft.AspNetCore.SignalR;
using ProtoBuf;
using Protocol;
using StackExchange.Redis;


namespace LobbyServer.BackgroundServices
{
    public class MatchResponseWorker : BackgroundService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IHubContext<SignalRHub> _hubContext;
        private readonly ILogger<MatchResponseWorker> _logger;

        // 구독할 레디스 채널명 (C++의 PUBLISH 채널과 100% 동일해야 함)
        private const string MatchCompleteChannel = "Channel:MatchComplete";

        public MatchResponseWorker(
            IConnectionMultiplexer redis,
            IHubContext<SignalRHub> hubContext,
            ILogger<MatchResponseWorker> logger)
        {
            _redis = redis;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("매칭 응답 수신 워커(MatchResponseWorker)가 시작되었습니다.");

            var subscriber = _redis.GetSubscriber();
            var db = _redis.GetDatabase();

            // C++ 서버가 발행(Publish)하는 채널 구독 (비동기 이벤트 대기)
            await subscriber.SubscribeAsync(MatchCompleteChannel, async (channel, message) =>
            {
                try
                {
                    GameRoomCreateResponse responseDto = GameRoomCreateResponse.Parser.ParseFrom((byte[])message);

                    _logger.LogInformation($"[방 생성 완료 수신] IP: {responseDto.Ip}, Port: {responseDto.Port}, 인원: {responseDto.Uids.Count}명");

                    // 병렬 처리를 위한 개선 코드
                    var sendTasks = responseDto.Uids.Select(async uid =>
                    {
                        var connectionId = await db.StringGetAsync($"SignalRConn:{uid}");
                        if (!connectionId.IsNullOrEmpty)
                        {
                            await _hubContext.Clients.Client(connectionId).SendAsync("MatchSuccess", new
                            {
                                Ip = responseDto.Ip,
                                Port = responseDto.Port
                            });
                            _logger.LogInformation($"[SignalR 발송 완료] UID: {uid} -> {connectionId}");
                        }
                        else
                        {
                            _logger.LogWarning($"[SignalR 발송 실패] UID: {uid}의 ConnectionId를 찾을 수 없습니다.");
                        }
                    });

                    // 모든 유저에게 동시에 비동기로 발송
                    await Task.WhenAll(sendTasks);
                }
                catch (Exception ex)
                {
                    // 역직렬화 실패 등 에러가 발생해도 워커(이벤트 리스너)가 죽지 않도록 방어
                    _logger.LogError($"매칭 응답 처리 중 오류 발생: {ex.Message}");
                }
            });

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // 정상 종료 시 발생하는 취소 예외 무시
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("매칭 응답 수신 워커가 종료됩니다. 구독을 해제합니다.");

            // 종료 시 메모리 누수 방지를 위해 구독 해제
            var subscriber = _redis.GetSubscriber();
            await subscriber.UnsubscribeAsync(MatchCompleteChannel);

            await base.StopAsync(stoppingToken);
        }
    }
}