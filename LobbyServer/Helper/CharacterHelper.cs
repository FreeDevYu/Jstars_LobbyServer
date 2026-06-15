using LobbyAPI;
using LobbyAPI.Models;
using LobbyServer.Models;
using LobbyServer.Repositories;
using Protocol;
using System.Text.Json;


namespace LobbyServer.Helper
{
    public interface ICharacterHelper
    {
        Task<List<Character>> GetAllCharactersByUIDAsync(long uid);
        Task ApplyMatchRewardsAsync(MatchingResultDTO result);
    }

    public class CharacterHelper : ICharacterHelper
    {
        private readonly ILobbyRespository _lobbyRespository;
        private readonly IUserRespository _userRespository;
        private readonly IRedisHelper _redisHelper;
        private readonly TimeSpan _characterDataExpiry = TimeSpan.FromHours(2);
        private const string PvpRecordRedisKey = "PvpRecord";

        public CharacterHelper(IRedisHelper redisHelper, ILobbyRespository lobbyRespository, IUserRespository userRespository)
        {
            _redisHelper = redisHelper;
            _lobbyRespository = lobbyRespository;
            _userRespository = userRespository;
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

        public async Task ApplyMatchRewardsAsync(MatchingResultDTO result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            // TODO: room_id 기준 idempotent 체크
            // if (await _lobbyRespository.IsMatchRewardAppliedAsync((long)result.RoomId))
            //     return;
            var rewards = GameRewardCalculator.CalculateAll(result).ToList();
            foreach (var reward in rewards)
            {
                await ApplyPlayerRewardAsync(result, reward);
            }
        }

        private async Task ApplyPlayerRewardAsync(MatchingResultDTO match, MatchPlayerReward reward)
        {
            //MatchingResultDTO에서 map,mode,death,kill등 정보를 활용해 보상계산 세분화 및 로그남기기 가능.

            await ApplyExpToCharacterCacheAsync(reward.Uid, reward.ExpReward);
            await ApplyGoldToUserCacheAsync(reward.Uid, reward.GoldReward);
            await ApplyPvpRecordAsync(reward.Uid, reward.IsWin);
        }

        private async Task ApplyPvpRecordAsync(long uid, bool isWin)
        {
            PvpRecord? record = await _lobbyRespository.IncrementPvpRecordAsync(uid, isWin);
            if (record == null)
                return;

            await _redisHelper.SetHashFieldsAsync(
                PvpRecordRedisKey,
                new Dictionary<string, string>
                {
                    { uid.ToString(), JsonSerializer.Serialize(record) }
                });
        }

        private async Task ApplyExpToCharacterCacheAsync(long uid, long expReward)
        {
            if (expReward <= 0)
                return;

            var characters = await GetAllCharactersByUIDAsync(uid);
            if (characters == null || characters.Count == 0)
                return;

            // TODO: 장착 캐릭터 / 최근 플레이 캐릭터 등 정책 결정
            Character target = characters[0];
            ApplyExpWithLevelUp(target, expReward);

            bool updated = await _lobbyRespository.UpdateCharacterLevelExpAsync(
                target.CharacterInstanceID,
                target.Level,
                target.Exp);

            if (!updated)
                return;

            string redisKey = $"character:{uid}";
            string field = target.CharacterInstanceID.ToString();
            string jsonValue = JsonSerializer.Serialize(target);

            await _redisHelper.SetHashFieldsAsync(
                redisKey,
                new Dictionary<string, string> { { field, jsonValue } },
                _characterDataExpiry);
        }

        private static void ApplyExpWithLevelUp(Character character, long expReward)
        {
            long totalExp = character.Exp + expReward;
            int levelGain = (int)(totalExp / CharacterLevelConstants.ExpPerLevel);
            long remainingExp = totalExp % CharacterLevelConstants.ExpPerLevel;

            character.Level += levelGain;
            character.Exp = remainingExp;
        }

        // account 데이터는 redis를 거치지 않고 즉시업데이트.
        private async Task ApplyGoldToUserCacheAsync(long uid, long goldReward)
        {
            if (goldReward <= 0)
                return;

            var user = await _userRespository.GetUserByUIDAsync(uid);
            user.Gold += goldReward;

            await _userRespository.UpdateAsync(user);
        }
    }
}