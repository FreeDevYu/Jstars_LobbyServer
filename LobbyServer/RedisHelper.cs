using StackExchange.Redis;

namespace LobbyAPI
{
    public interface IRedisHelper
    {
        Task<bool> SetKeyValueAsync(string key, string value, TimeSpan? expiry = null);
        Task<long> EnqueueKeyValueAsync(string key, string value);
        Task<string> GetValueAsync(string key);

        Task<bool> SetHashFieldsAsync(string key, Dictionary<string, string> fields, TimeSpan? expiry = null);
        Task<long> DeleteHashFieldsAsync(string key, params string[] fields);
        Task<bool> DeleteHashFieldAsync(string key, string field);

        Task<string> GetHashFieldAsync(string key, string field);
        Task<Dictionary<string, string>> GetAllHashFieldsAsync(string key);
        Task<long> IncrementHashFieldAsync(string key, string field, long value = 1);

        Task<bool> RemoveDataByKeyAsync(string key);

        Task<bool> AcquireLockAsync(string key, string token, TimeSpan expiry);
        Task<bool> ReleaseLockAsync(string key, string token);
    }

    public class RedisHelper : IRedisHelper
    {
        private readonly IDatabase _redis;

        public RedisHelper(IConnectionMultiplexer redis)
        {
            _redis = redis.GetDatabase();
        }


        //string패턴 = key,value구조
        public async Task<bool> SetKeyValueAsync(string key, string value, TimeSpan? expiry = null)
        {
            if (expiry.HasValue)
            {
                return await _redis.StringSetAsync(key, value, expiry.Value);
            }
            else
            {
                return await _redis.StringSetAsync(key, value);// 영구 저장
            }
        }

        // Redis List의 오른쪽에 데이터 삽입 (RPUSH)
        // O(1)의 속도로 매우 빠르게 삽입됩니다.
        public async Task<long> EnqueueKeyValueAsync(string key, string value)
        {
            long currentQueueCount = await _redis.ListRightPushAsync(key, value);
            return currentQueueCount;
        }

        public async Task<string> GetValueAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            // StackExchange.Redis의 핵심 함수: StringGetAsync
            // RedisValue 타입을 반환하는데, string으로 암시적 형변환이 가능해.
            RedisValue result = await _redis.StringGetAsync(key);

            // 데이터가 없으면 result.HasValue가 false가 됨
            if (!result.HasValue)
            {
                return string.Empty;
            }

            return result.ToString();
        }

        public async Task<bool> SetHashFieldsAsync(string key, Dictionary<string, string> fields, TimeSpan? expiry = null)
        {
            // Dictionary를 Redis의 HashEntry 배열로 변환
            var hashEntries = fields.Select(f => new HashEntry(f.Key, f.Value)).ToArray();

            // HashSetAsync는 여러 필드를 한 번에 저장합니다 (HMSET 역할)
            await _redis.HashSetAsync(key, hashEntries);

            // 만료 시간이 있다면 Key 전체에 만료 시간을 설정
            if (expiry.HasValue)
            {
                return await _redis.KeyExpireAsync(key, expiry.Value);
            }

            return true;
        }

        public async Task<string> GetHashFieldAsync(string key, string field)
        {
            RedisValue result = await _redis.HashGetAsync(key, field);
            return result.HasValue ? result.ToString() : string.Empty;
        }

        public async Task<Dictionary<string, string>> GetAllHashFieldsAsync(string key)
        {
            HashEntry[] hashEntries = await _redis.HashGetAllAsync(key);

            return hashEntries.ToDictionary(
                entry => entry.Name.ToString(),
                entry => entry.Value.ToString()
            );
        }

        //데이터가없으면 count가0인 dic이 반환된다.
        public async Task<long> IncrementHashFieldAsync(string key, string field, long value = 1)
        {
            // 다른 스레드나 서버가 개입할 틈 없이 즉각적으로 더해지고 결과가 반환됩니다.
            return await _redis.HashIncrementAsync(key, field, value);
        }

        public async Task<bool> RemoveDataByKeyAsync(string key)
        {
            bool success = await _redis.KeyDeleteAsync(key);
            return success;
        }

        // 여러 개의 필드를 한 번에 삭제하는 함수 (params 키워드 활용)
        public async Task<long> DeleteHashFieldsAsync(string key, params string[] fields)
        {
            if (fields == null || fields.Length == 0)
                return 0;

            // string 배열을 RedisValue 배열로 변환
            var redisFields = fields.Select(f => (RedisValue)f).ToArray();

            // HashDeleteAsync는 실제로 삭제에 성공한 필드의 개수를 반환합니다 (HDEL 역할)
            long deletedCount = await _redis.HashDeleteAsync(key, redisFields);

            return deletedCount;
        }

        // 단일 필드만 삭제하는 함수 (오버로딩)
        public async Task<bool> DeleteHashFieldAsync(string key, string field)
        {
            // 단일 필드 삭제 시에는 삭제 성공 여부를 bool로 반환합니다.
            return await _redis.HashDeleteAsync(key, field);
        }

        public async Task<bool> AcquireLockAsync(string key, string token, TimeSpan expiry)
        {
            // LockTakeAsync는 Key가 존재하지 않을 때만 생성하고(NX), 만료시간을 설정하며,
            // Value로 토큰값을 저장하는 과정을 원자적으로(Atomic) 처리합니다.
            return await _redis.LockTakeAsync(key, token, expiry);
        }

        public async Task<bool> ReleaseLockAsync(string key, string token)
        {
            // LockReleaseAsync는 내부적으로 Lua 스크립트를 사용하여,
            // "해당 키의 Value가 내가 넘긴 Token과 일치할 때만 삭제(DEL)"하도록 안전하게 처리합니다.
            return await _redis.LockReleaseAsync(key, token);
        }
    }
}