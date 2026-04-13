using LobbyAPI.Models;
using LobbyServer.Hubs; 
using Microsoft.AspNetCore.SignalR;
using ProtoBuf;
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
                    // c++ field server에서는 바이너리 데이터 Protobuf 객체로 저장되어있음.
                    GameRoomCreateResponse responseDto;
                    using (var ms = new MemoryStream((byte[])message))
                    {
                        responseDto = Serializer.Deserialize<GameRoomCreateResponse>(ms);
                    }
                    _logger.LogInformation($"[방 생성 완료 수신] IP: {responseDto.Ip}, Port: {responseDto.Port}, 인원: {responseDto.UIDList.Count}명");

                    // 2. 방에 배정된 유저들에게 SignalR로 응답 전송
                    foreach (var uid in responseDto.UIDList)
                    {
                        // 유저의 고유 ConnectionId 조회
                        var connectionId = await db.StringGetAsync($"SignalRConn:{uid}");

                        if (!connectionId.IsNullOrEmpty)
                        {
                            // 클라이언트의 'MatchSuccess' 콜백 함수 호출 및 접속 정보 전달
                            await _hubContext.Clients.Client(connectionId).SendAsync("MatchSuccess", new
                            {
                                Ip = responseDto.Ip,
                                Port = responseDto.Port
                            });

                            _logger.LogInformation($"[SignalR 발송 완료] UID: {uid} -> {connectionId}");
                        }
                        else
                        {
                            _logger.LogWarning($"[SignalR 발송 실패] UID: {uid}의 ConnectionId를 찾을 수 없습니다. (매칭 중 접속 종료 의심)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 역직렬화 실패 등 에러가 발생해도 워커(이벤트 리스너)가 죽지 않도록 방어
                    _logger.LogError($"매칭 응답 처리 중 오류 발생: {ex.Message}");
                }
            });

            // 💡 중요: 이벤트 기반(구독) 워커이므로 while 루프 없이 Task.Delay로 프로세스만 무한 대기시킵니다.
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