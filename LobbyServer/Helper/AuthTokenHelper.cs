using Google.Protobuf;
using LobbyAPI;
using LobbyServer.Models; // AuthTokenDTO가 있는 네임스페이스
using ProtoBuf;           // protobuf-net 라이브러리 추가
using System;
using System.IO;          // MemoryStream 사용을 위해 추가
using System.Threading.Tasks;

namespace LobbyServer.Helper
{
    public interface IAuthTokenHelper
    {
        Task<string?> CreateTokenAsync(User user, string deviceId, string tokenString);
        Task<bool> RevokeTokenAsync(string id);
        // 반환형이 AuthTokenDTO로 변경됨
        Task<Protocol.AuthTokenDTO?> GetTokenAsync(string id, string clientToken);
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
            // 1. DateTime 대신 Unix Timestamp (초 단위) 생성
            long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long expiresUnix = DateTimeOffset.UtcNow.Add(_tokenExpiry).ToUnixTimeSeconds();

            // 2. DTO 객체 생성 (필요시 ulong 캐스팅 적용)
            var token = new Protocol.AuthTokenDTO
            {
                Token = tokenString,
                Uid = user.UID,           // 타입이 안 맞는다면 (ulong) 추가
                DeviceId = deviceId,
                CreatedAt = nowUnix,      // 타입이 안 맞는다면 (ulong) 추가
                ExpiresAt = expiresUnix   // 타입이 안 맞는다면 (ulong) 추가
            };

            string redisKey = $"auth:token:{user.UID}";

            // 3. 구글 Protobuf 방식의 초간단 직렬화 (이전의 MemoryStream 코드 통째로 대체!)
            byte[] protoBytes = token.ToByteArray();

            // 4. Redis 저장
            bool saved = await _redisHelper.SetKeyValueAsync(redisKey, protoBytes, _tokenExpiry);
            if (!saved)
            {
                return null;
            }

            return tokenString;
        }

        public async Task<Protocol.AuthTokenDTO?> GetTokenAsync(string id, string clientToken)
        {
            string redisKey = $"auth:token:{id}";

            byte[]? byteData = await _redisHelper.GetBinaryValueAsync(redisKey);

            if (byteData == null || byteData.Length == 0)
            {
                return null; // 세션이 만료되었거나 존재하지 않음
            }

            try
            {
                Protocol.AuthTokenDTO authToken = Protocol.AuthTokenDTO.Parser.ParseFrom(byteData);

                // 2. 토큰 일치 확인
                if (authToken.Token != clientToken)
                {
                    return null;
                }

                if (authToken.Token != clientToken)
                {
                    return null; // 다른 기기 로그인으로 밀려남
                }

                long currentUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (authToken.ExpiresAt < currentUnix)
                {
                    return null;
                }

                return authToken;
            }
            catch (Exception) // 역직렬화 실패 시 (데이터 손상 등)
            {
                return null;
            }
        }

        public async Task<bool> RevokeTokenAsync(string id)
        {
            string redisKey = $"auth:token:{id}";
            return await _redisHelper.DeleteKeyAsync(redisKey);
        }
    }
}