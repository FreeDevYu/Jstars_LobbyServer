using LobbyAPI;
using LobbyAPI.Models;
using LobbyServer.Repositories;
using System.Text.Json;


namespace LobbyServer.LobbyService
{
    public interface ICharacterHelper
    {
        Task<List<Character>> GetAllCharactersByUIDAsync(long uid);
    }

    public class CharacterHelper : ICharacterHelper
    {
        private readonly ILobbyRespository _lobbyRespository;
        private readonly IRedisHelper _redisHelper;
        private readonly TimeSpan _characterDataExpiry = TimeSpan.FromHours(2);

        public CharacterHelper(IRedisHelper redisHelper, ILobbyRespository lobbyRespository)
        {
            _redisHelper = redisHelper;
            _lobbyRespository = lobbyRespository;
        }

        public async Task<List<Character>> GetAllCharactersByUIDAsync(long uid)
        {
            List<Character> result = null;
            string redisKey = $"character:{uid}";

            // 1. Redis에서 캐싱 데이터 가져오기 (Dictionary<string, string>)
            // Key: CharacterInstanceID (문자열), Value: 캐릭터 JSON 데이터
            var cachingData = await _redisHelper.GetAllHashFieldsAsync(redisKey);

            // 2. Redis에 캐싱된 데이터가 있을 경우: 최적화해서 List로 뽑아내기
            if (cachingData != null && cachingData.Count > 0)
            {
                // Dictionary의 Value(JSON 문자열)들만 뽑아서 곧바로 Character 객체로 역직렬화
                result = cachingData.Values
                                  .Select(json => JsonSerializer.Deserialize<Character>(json))
                                  .ToList();
                return result;
            }

            // 3. Redis에 캐싱된 데이터가 없을 경우: DB 조회 및 Redis 캐싱
            var dbData = await _lobbyRespository.GetAllCharactersByUIDAsync(uid);
            result = dbData.ToList();

            if (result.Count > 0)
            {
                var hashEntries = new Dictionary<string, string>();

                foreach (var character in result)
                {
                    // Field = 캐릭터 ID, Value = 캐릭터 객체의 JSON 문자열
                    string field = character.CharacterInstanceID.ToString();
                    string jsonValue = JsonSerializer.Serialize(character);

                    hashEntries.Add(field, jsonValue);
                }

                // for문 밖에서 단 한 번만 통신하여 Redis에 모든 캐릭터를 일괄 저장 (네트워크 최적화)
                await _redisHelper.SetHashFieldsAsync(redisKey, hashEntries, _characterDataExpiry);
            }

            return result;
        }
    }
}

//할일
/*
 1. 장비리스트 전달.
 2. 닉네임변경요청
 -> 클라이언트와 로비서버 연결필요
 3. pvp 게임룸생성 -> 로비서버가 필드서버와 연락필요
 4. pve 진행요청
 5. 
 */