using LobbyServer.Helper;
using LobbyServer.Hubs;
using Microsoft.AspNetCore.SignalR;
using Protocol;
using StackExchange.Redis;

namespace LobbyServer.BackgroundServices
{
    public class PveMatchingRequestWorker : BackgroundService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<SignalRHub> _hubContext;
        private readonly ILogger<PveMatchingRequestWorker> _logger;

        private const string QueueKey = "PveMatchingQueue_ZSET";
        private const double MATCH_RANGE = 5.0;
        private const int MATCH_USER_COUNT = 1;

        private readonly LuaScript _matchingScript;

        public PveMatchingRequestWorker(
            IConnectionMultiplexer redis,
            IServiceScopeFactory scopeFactory,
            IHubContext<SignalRHub> hubContext,
            ILogger<PveMatchingRequestWorker> logger)
        {
            _redis = redis;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _logger = logger;

            _matchingScript = MatchingLuaScripts.MatchPlayers;
        }

        public async Task<List<long>?> TryMatchAsync(int n, double range)
        {
            var db = _redis.GetDatabase();

            var result = await _matchingScript.EvaluateAsync(db, new
            {
                queueKey = (RedisKey)QueueKey,
                n = n,
                range = range,
                limit = 50
            });

            if (result.IsNull) return null;

            return ((RedisValue[])result).Select(v => (long)v).ToList();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PVE 매칭 워커가 시작되었습니다.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var matchedUids = await TryMatchAsync(MATCH_USER_COUNT, MATCH_RANGE);

                    if (matchedUids != null && matchedUids.Count == MATCH_USER_COUNT)
                    {
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var matchingHelper = scope.ServiceProvider.GetRequiredService<IMatchingHelper>();
                            bool success = await matchingHelper.CachingPlayerData(matchedUids);
                            if (success)
                            {
                                success = await matchingHelper.CreateRoomAsync(matchedUids, 1, GameModeType.GameModePve);
                            }

                            if (success)
                            {
                                _logger.LogInformation("[PVE 매칭 성사] UID: {Uids}", string.Join(", ", matchedUids));
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "[PVE 매칭 실패] UID: {Uids} (플레이어 데이터 캐싱 또는 방 생성 실패)",
                                    string.Join(", ", matchedUids));
                            }
                        }
                    }
                    else
                    {
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PVE 매칭 처리 중 오류 발생: {Message}", ex.Message);
                    await Task.Delay(2000, stoppingToken);
                }
            }

            _logger.LogInformation("PVE 매칭 워커가 종료되었습니다.");
        }
    }
}
