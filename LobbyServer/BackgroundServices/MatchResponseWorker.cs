using LobbyAPI.Models;
using LobbyServer.Hubs; 
using LobbyServer.Services;
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
        private readonly IMatchLatencyMetrics _matchLatencyMetrics;

        // 구독할 레디스 채널명 (C++의 PUBLISH 채널과 100% 동일해야 함)
        private const string MatchCompleteChannel = "Channel:MatchComplete";

        public MatchResponseWorker(
            IConnectionMultiplexer redis,
            IHubContext<SignalRHub> hubContext,
            ILogger<MatchResponseWorker> logger,
            IMatchLatencyMetrics matchLatencyMetrics)
        {
            _redis = redis;
            _hubContext = hubContext;
            _logger = logger;
            _matchLatencyMetrics = matchLatencyMetrics;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("매칭 응답 수신 워커 시작.");

            var subscriber = _redis.GetSubscriber();
            var db = _redis.GetDatabase();

            // 1. 구독 시작
            await subscriber.SubscribeAsync(MatchCompleteChannel, (channel, message) =>
            {
                // 콜백 내부에서 직접 async 로직을 실행하기 위해 Task.Run 권장 (선택사항)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        GameRoomCreateResponse responseDto = GameRoomCreateResponse.Parser.ParseFrom((byte[])message);
                        _logger.LogInformation(
                            $"[방 생성 수신] RoomID: {responseDto.Roomid}, MapID: {responseDto.Mapid}, GameMode: {responseDto.GameMode}, Uids: {string.Join(", ", responseDto.Uids)}");

                        // Redis Cluster: multi-key MGET는 서로 다른 slot이면 실패 (PVP 2인 매칭)
                        var sendTasks = responseDto.Uids.Select(async uid =>
                        {
                            var connectionId = await db.StringGetAsync((RedisKey)$"SignalRConn:{uid}");
                            if (connectionId.IsNullOrEmpty)
                            {
                                await _matchLatencyMetrics.CancelPendingAsync((long)uid);
                                _logger.LogWarning("[MatchSuccess] 전송 스킵: SignalR 미연결. Uid={Uid}", uid);
                                return;
                            }

                            await _hubContext.Clients.Client(connectionId!).SendAsync("MatchSuccess", new
                            {
                                Ip = responseDto.Ip,
                                Port = responseDto.Port,
                                RoomID = responseDto.Roomid,
                                MapID = responseDto.Mapid,
                                GameMode = (int)responseDto.GameMode
                            }, stoppingToken);

                            await _matchLatencyMetrics.TryRecordSuccessAsync((long)uid);
                        });

                        await Task.WhenAll(sendTasks);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"처리 중 오류: {ex.Message}");
                    }
                }, stoppingToken);
            });

            // 3. 종료 대기 및 자원 해제
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("매칭 워커 종료 중... 구독을 해제합니다.");
                await subscriber.UnsubscribeAsync(MatchCompleteChannel); // 명시적 구독 해제
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