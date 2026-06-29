using LobbyServer.Helper;
using LobbyServer.Hubs;
using LobbyServer.Services;
using Microsoft.AspNetCore.SignalR;
using Protocol;
using StackExchange.Redis;

namespace LobbyServer.BackgroundServices
{
    public class MatchingRequestWorker : BackgroundService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<SignalRHub> _hubContext;
        private readonly ILogger<MatchingRequestWorker> _logger; // 로거 주입 권장
        private readonly IMatchLatencyMetrics _matchLatencyMetrics;

        private const string QueueKey = "MatchingQueue_ZSET";
        //private const double MATCH_RANGE = 5.0; // 승률 오차범위 ±5% 이내 매칭
        private const double MATCH_RANGE = 100.0; // 임시: 승률 무시 (0~100 전부 허용)
        private const int MATCH_USER_COUNT = 2;

        // Lua 스크립트 캐싱 객체
        private readonly LuaScript _matchingScript;

        public MatchingRequestWorker(
            IConnectionMultiplexer redis,
            IServiceScopeFactory scopeFactory,
            IHubContext<SignalRHub> hubContext,
            ILogger<MatchingRequestWorker> logger,
            IMatchLatencyMetrics matchLatencyMetrics)
        {
            _redis = redis;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _logger = logger;
            _matchLatencyMetrics = matchLatencyMetrics;

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
            _logger.LogInformation("매칭 워커가 시작되었습니다.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try // 예외 발생 시 워커 종료 방지
                {
                    var matchedUids = await TryMatchAsync(MATCH_USER_COUNT, MATCH_RANGE);

                    if (matchedUids != null && matchedUids.Count == MATCH_USER_COUNT)
                    {
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var matchingHelper = scope.ServiceProvider.GetRequiredService<IMatchingHelper>();
                            bool success = await matchingHelper.CachingPlayerData(matchedUids);
                            if(success)
                            {
                                success = await matchingHelper.CreateRoomAsync(matchedUids, 2, GameModeType.GameModePvp);
                            }

                            if (success)
                            {
                                _logger.LogInformation("[PvP 매칭 성사] UID: {Uids}", string.Join(", ", matchedUids));
                            }
                            else
                            {
                                await _matchLatencyMetrics.CancelPendingManyAsync(matchedUids);
                                _logger.LogWarning(
                                    "[PvP 매칭 실패] UID: {Uids} (플레이어 데이터 캐싱 또는 방 생성 실패)",
                                    string.Join(", ", matchedUids));
                            }
                        }
                    }
                    else
                    {
                        // 큐에 매칭 가능한 대상이 없으면 1초 대기 후 재탐색
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (TaskCanceledException)
                {
                    // 서버 종료 시 발생하는 정상적인 취소 예외는 무시
                    break;
                }
                catch (System.Exception ex)
                {
                    _logger.LogError($"매칭 처리 중 오류 발생: {ex.Message}");
                    // 장애 시 잠시 대기 후 재시도 (무한 에러 루프 방지)
                    await Task.Delay(2000, stoppingToken);
                }
            }

            _logger.LogInformation("매칭 워커가 종료되었습니다.");
        }
    }
}