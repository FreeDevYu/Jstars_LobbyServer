using LobbyAPI;
using LobbyServer.Models;
using System.Text.Json;

namespace LobbyServer.AuthService
{
    public interface IAuthTokenHelper
    {
        Task<string?> CreateTokenAsync(User user, string deviceId, string tokenString);
        Task<bool> RevokeTokenAsync(string id);
        Task<AuthToken?> GetTokenAsync(string id, string clientToken);

        //Task<AuthToken> GetTokenAsync(string token);
        //Task<bool> ValidateTokenAsync(string token);
        //Task<bool> RevokeTokenAsync(string token);
        //Task<IEnumerable<AuthToken>> GetUserTokensAsync(int userId);
        //Task<bool> RevokeAllUserTokensAsync(int userId);
    }
    public class AuthTokenHelper : IAuthTokenHelper
    {
        private readonly IRedisHelper _redisHelper;
        private readonly TimeSpan _tokenExpiry = TimeSpan.FromDays(1); // 토큰 기본 유효 기간: 1일

        public AuthTokenHelper(IRedisHelper redis)
        {
            _redisHelper = redis;
        }

        public async Task<string?> CreateTokenAsync(User user, string deviceId, string tokenString)
        {
            // 토큰 객체 생성
            var token = new AuthToken
            {
                Token = tokenString,
                UID = user.UID,
                DeviceID = deviceId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(_tokenExpiry)
            };

            // Redis에 토큰 저장
            string redisKey = $"auth:token:{user.ID}";
            string jsonValue = JsonSerializer.Serialize(token); // 접두어 없이 순수 JSON만

            bool saved = await _redisHelper.SetKeyValueAsync(redisKey, jsonValue, _tokenExpiry);
            if (!saved)
            {
                return null;
            }

            return tokenString;
        }

        public async Task<AuthToken?> GetTokenAsync(string id, string clientToken)
        {
            // 1. UID를 키로 사용하여 Redis에서 데이터를 가져옴
            string redisKey = $"auth:token:{id}";
            var jsonData = await _redisHelper.GetValueAsync(redisKey);

            if (string.IsNullOrEmpty(jsonData))
            {
                return null; // 세션이 만료되었거나 존재하지 않음
            }

            // 2. JSON을 객체로 복구
            var authToken = JsonSerializer.Deserialize<AuthToken>(jsonData);
            if (authToken == null) return null;

            // 3. [핵심] 클라이언트가 보낸 토큰과 Redis에 저장된 최신 토큰이 일치하는지 비교
            if (authToken.Token != clientToken)
            {
                // 토큰이 다르다는 건 다른 기기에서 새로 로그인해서 '밀려났다'는 뜻!
                return null;
            }

            // 4. 만료 시간 체크
            if (authToken.ExpiresAt < DateTime.UtcNow)
            {
                //await _redisHelper.DeleteKeyAsync(redisKey);
                return null;
            }

            return authToken;
        }

        public async Task<bool> RevokeTokenAsync(string id)
        {
            string redisKey = $"auth:token:{id}";
            bool deleted = await _redisHelper.DeleteKeyAsync(redisKey);
            return deleted;
        }
    }

    public class AuthToken
    {
        public string Token { get; set; } = string.Empty;
        public long UID { get; set; }
        public string DeviceID { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}
