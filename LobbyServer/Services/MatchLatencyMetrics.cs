using StackExchange.Redis;

namespace LobbyServer.Services
{
    public interface IMatchLatencyMetrics
    {
        Task RecordEnqueueAsync(long uid);
        Task CancelPendingAsync(long uid);
        Task CancelPendingManyAsync(IEnumerable<long> uids);
        Task TryRecordSuccessAsync(long uid);
        Task<(long SumMs, long Count)> GetStatsAsync();
    }

    /// <summary>
    /// Enqueue 시각 ~ MatchSuccess 전송까지 E2E 대기(ms). 취소·실패 시 pending만 제거(집계 제외).
    /// </summary>
    public sealed class MatchLatencyMetrics : IMatchLatencyMetrics
    {
        private const string PendingKeyPrefix = "match:pending:";
        private const string SumKey = "monitor:match:latency:sum_ms";
        private const string CountKey = "monitor:match:latency:count";
        private static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(10);

        private readonly IDatabase _db;

        public MatchLatencyMetrics(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        public Task RecordEnqueueAsync(long uid)
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return _db.StringSetAsync(PendingKey(uid), nowMs.ToString(), PendingTtl);
        }

        public Task CancelPendingAsync(long uid) =>
            _db.KeyDeleteAsync(PendingKey(uid));

        public async Task CancelPendingManyAsync(IEnumerable<long> uids)
        {
            var keys = uids.Select(uid => (RedisKey)PendingKey(uid)).ToArray();
            if (keys.Length == 0)
                return;

            await _db.KeyDeleteAsync(keys);
        }

        public async Task TryRecordSuccessAsync(long uid)
        {
            RedisKey key = PendingKey(uid);
            RedisValue raw = await _db.StringGetAsync(key);
            if (raw.IsNullOrEmpty || !long.TryParse(raw.ToString(), out long enqueueMs))
                return;

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long deltaMs = Math.Max(0, nowMs - enqueueMs);

            await _db.StringIncrementAsync(SumKey, deltaMs);
            await _db.StringIncrementAsync(CountKey, 1);
            await _db.KeyDeleteAsync(key);
        }

        public async Task<(long SumMs, long Count)> GetStatsAsync()
        {
            var sumTask = _db.StringGetAsync(SumKey);
            var countTask = _db.StringGetAsync(CountKey);
            await Task.WhenAll(sumTask, countTask);

            long sum = long.TryParse(sumTask.Result.ToString(), out long s) ? s : 0;
            long count = long.TryParse(countTask.Result.ToString(), out long c) ? c : 0;
            return (sum, count);
        }

        private static string PendingKey(long uid) => $"{PendingKeyPrefix}{uid}";
    }
}
