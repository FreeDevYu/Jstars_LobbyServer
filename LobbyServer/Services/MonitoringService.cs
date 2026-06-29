using LobbyAPI.Models;
using LobbyServer.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace LobbyServer.Services
{
    public record FieldServerUserInfo(string ServerName, int UserCount);

    public record MonitoringSummary(
        long SignalRConnections,
        long AuthTokens,
        long PvpMatchingQueue,
        long PveMatchingQueue,
        IReadOnlyList<FieldServerUserInfo> FieldServers,
        int FieldServerTotalUsers,
        bool RedisConnected,
        double? MatchLatencyAverageMs,
        long MatchLatencySampleCount,
        DateTime? RankingLastRefreshedUtc,
        DateTime TimestampUtc);

    public interface IMonitoringService
    {
        Task<MonitoringSummary> GetSummaryAsync();
    }

    public class MonitoringService : IMonitoringService
    {
        private const string SignalRConnPattern = "SignalRConn:*";
        private const string AuthTokenPattern = "auth:token:*";
        private const string PvpQueueKey = "MatchingQueue_ZSET";
        private const string PveQueueKey = "PveMatchingQueue_ZSET";
        private const string FieldServerUserKey = "FIELD_SERVER_CURRENT_USER";

        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private readonly IMatchLatencyMetrics _matchLatencyMetrics;

        public MonitoringService(IConnectionMultiplexer redis, IMatchLatencyMetrics matchLatencyMetrics)
        {
            _redis = redis;
            _db = redis.GetDatabase();
            _matchLatencyMetrics = matchLatencyMetrics;
        }

        public async Task<MonitoringSummary> GetSummaryAsync()
        {
            var signalRTask = CountKeysByPatternAsync(SignalRConnPattern);
            var authTokenTask = CountKeysByPatternAsync(AuthTokenPattern);
            var pvpTask = _db.SortedSetLengthAsync(PvpQueueKey);
            var pveTask = _db.SortedSetLengthAsync(PveQueueKey);
            var fieldServersTask = GetFieldServersAsync();
            var latencyTask = _matchLatencyMetrics.GetStatsAsync();
            var rankingMetaTask = _db.StringGetAsync(RankingConstants.MetaKey);

            await Task.WhenAll(signalRTask, authTokenTask, pvpTask, pveTask, fieldServersTask, latencyTask, rankingMetaTask);

            var fieldServers = await fieldServersTask;
            var (sumMs, sampleCount) = await latencyTask;
            double? averageMs = sampleCount > 0 ? (double)sumMs / sampleCount : null;
            DateTime? rankingRefreshed = ParseRankingRefreshedAt(await rankingMetaTask);

            return new MonitoringSummary(
                await signalRTask,
                await authTokenTask,
                await pvpTask,
                await pveTask,
                fieldServers,
                fieldServers.Sum(s => s.UserCount),
                _redis.IsConnected,
                averageMs,
                sampleCount,
                rankingRefreshed,
                DateTime.UtcNow);
        }

        private static DateTime? ParseRankingRefreshedAt(RedisValue raw)
        {
            if (raw.IsNullOrEmpty)
                return null;

            try
            {
                using var doc = JsonDocument.Parse(raw.ToString());
                if (doc.RootElement.TryGetProperty("refreshedAt", out var prop) &&
                    prop.TryGetDateTime(out DateTime refreshedAt))
                {
                    return refreshedAt.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(refreshedAt, DateTimeKind.Utc)
                        : refreshedAt.ToUniversalTime();
                }
            }
            catch (JsonException)
            {
            }

            return null;
        }

        private async Task<IReadOnlyList<FieldServerUserInfo>> GetFieldServersAsync()
        {
            var entries = await _db.SortedSetRangeByRankWithScoresAsync(
                FieldServerUserKey, 0, -1, Order.Descending);

            if (entries.Length == 0)
                return Array.Empty<FieldServerUserInfo>();

            return entries
                .Select(e => new FieldServerUserInfo(e.Element.ToString(), (int)e.Score))
                .ToList();
        }

        private async Task<long> CountKeysByPatternAsync(string pattern)
        {
            // 동일 Redis 인스턴스에 엔드포인트가 2개 잡히면(예: 127.0.0.1 + ::1) 키가 이중 집계됨
            var uniqueKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var endpoint in _redis.GetEndPoints())
            {
                var server = _redis.GetServer(endpoint);
                if (!server.IsConnected || server.IsReplica)
                    continue;

                await foreach (var key in server.KeysAsync(pattern: pattern))
                {
                    uniqueKeys.Add(key.ToString());
                }
            }

            return uniqueKeys.Count;
        }
    }
}
