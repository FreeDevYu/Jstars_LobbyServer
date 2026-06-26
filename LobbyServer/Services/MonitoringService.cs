using StackExchange.Redis;

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

        public MonitoringService(IConnectionMultiplexer redis)
        {
            _redis = redis;
            _db = redis.GetDatabase();
        }

        public async Task<MonitoringSummary> GetSummaryAsync()
        {
            var signalRTask = CountKeysByPatternAsync(SignalRConnPattern);
            var authTokenTask = CountKeysByPatternAsync(AuthTokenPattern);
            var pvpTask = _db.SortedSetLengthAsync(PvpQueueKey);
            var pveTask = _db.SortedSetLengthAsync(PveQueueKey);
            var fieldServersTask = GetFieldServersAsync();

            await Task.WhenAll(signalRTask, authTokenTask, pvpTask, pveTask, fieldServersTask);

            var fieldServers = await fieldServersTask;
            return new MonitoringSummary(
                await signalRTask,
                await authTokenTask,
                await pvpTask,
                await pveTask,
                fieldServers,
                fieldServers.Sum(s => s.UserCount),
                DateTime.UtcNow);
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
            long count = 0;

            foreach (var endpoint in _redis.GetEndPoints())
            {
                var server = _redis.GetServer(endpoint);
                if (!server.IsConnected || server.IsReplica)
                    continue;

                await foreach (var _ in server.KeysAsync(pattern: pattern))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
