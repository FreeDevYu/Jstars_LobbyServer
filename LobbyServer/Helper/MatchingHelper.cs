using LobbyAPI;
using LobbyAPI.Models;
using LobbyServer.Repositories;
using Google.Protobuf;
using StackExchange.Redis;
using System.Text.Json;
using LobbyServer.Models;

namespace LobbyServer.Helper
{
    public interface IMatchingHelper
    {
        Task<bool> EnqueueMatchingQueue(long uid);
        Task<bool> CancelMatchingQueue(long uid);
        Task<bool> CreateRoomAsync(List<long> uidList, int teamCount);
        Task<bool> CachingPlayerData(List<long> uidList);
    }

    public class MatchingHelper : IMatchingHelper
    {
        private readonly ILobbyRespository _lobbyRespository;
        private readonly IRedisHelper _redisHelper;
        private readonly string _key = "MatchingQueue_ZSET";
        private readonly string _fieldServerCurrentKey = "FIELD_SERVER_CURRENT_USER";


        public MatchingHelper(IRedisHelper redisHelper, ILobbyRespository lobbyRespository)
        {
            _lobbyRespository = lobbyRespository;
            _redisHelper = redisHelper;
        }

        public async Task<bool> EnqueueMatchingQueue(long uid)
        {
            double score = 50.0; // 기본값 (전적이 없을 경우)
            string redisKey = "PvpRecord";
            PvpRecord? pvpRecord = null;

            //  Redis 캐시 확인
            string jsonValue = await _redisHelper.GetHashFieldAsync(redisKey, uid.ToString());

            if (string.IsNullOrEmpty(jsonValue))
            {
                // 캐시 없으면 DB 조회
                pvpRecord = await _lobbyRespository.GetPvpRecordByUIDAsync(uid);

                if (pvpRecord != null)
                {
                    var hashEntries = new Dictionary<string, string> { { uid.ToString(), JsonSerializer.Serialize(pvpRecord) } };
                    await _redisHelper.SetHashFieldsAsync(redisKey, hashEntries);
                }
            }
            else
            {
                pvpRecord = JsonSerializer.Deserialize<PvpRecord>(jsonValue);
            }

            if (pvpRecord != null && pvpRecord.Total > 0)
            {
                score = (double)pvpRecord.Win / pvpRecord.Total * 100.0;
            }

            return await _redisHelper.AddZSetAsync(_key, uid, score);
        }

        public async Task<bool> CancelMatchingQueue(long uid)
        {
            return await _redisHelper.RemoveZSetAsync(_key, uid);
        }

        public async Task<bool> CreateRoomAsync(List<long> uidList, int teamCount)
        {
            //TODO
            int gameMapId = 1;

            // 1. 가장 인원이 적은 서버 찾기 (로드밸런싱)
            RedisValue[] serverList = await _redisHelper.GetZSetRangeAsync(
                _fieldServerCurrentKey, start: 0, stop: 0, order: Order.Ascending);

            if (serverList.Length < 1)
                return false;

            string targetServerName = serverList[0].ToString();

            // 2. DTO 생성 및 맵 ID 세팅
            Protocol.GameRoomCreateDTO dto = new Protocol.GameRoomCreateDTO
            {
                GameMapId = gameMapId
            };

            // 3. 팀 분배 및 PlayerInfo 객체 추가
            for (int i = 0; i < uidList.Count; i++)
            {
                // teamCount가 1 이하면(개인전/데스매치) 모두 0, 아니면 1팀, 2팀... 순차 배분
                int assignedTeamId = 0;
                if (teamCount > 1)
                {
                    // 예: 6명, 2팀이면 1, 2, 1, 2, 1, 2 형태로 골고루 분배됩니다.
                    assignedTeamId = i % teamCount + 1;
                }

                dto.Players.Add(new Protocol.PlayerInfo
                {
                    Uid = (ulong)uidList[i],
                    TeamId = assignedTeamId
                });
            }

            // 4. 직렬화 (MemoryStream 없이 간단하게 byte 배열로 변환)
            byte[] protoBytes = dto.ToByteArray();

            // 5. Redis 인게임 서버 매칭 큐에 넣기
            string redisKey = $"MatchingQueue:{targetServerName}";
            long count = await _redisHelper.EnqueueKeyValueAsync(redisKey, protoBytes);

            return count > 0;
        }

        public async Task<bool> CachingPlayerData(List<long> uidList)
        {
            bool isAllSuccess = true;

            foreach (long uid in uidList)
            {
                // 1. Redis에서 캐릭터와 인벤토리 데이터를 동시에 조회 (병렬 처리로 속도 최적화)
                var userTask = _redisHelper.GetValueAsync($"user:{uid}");
                var characterTask = _redisHelper.GetAllHashFieldsAsync($"character:{uid}");
                var inventoryTask = _redisHelper.GetAllHashFieldsAsync($"inventory:{uid}");

                await Task.WhenAll(userTask, characterTask, inventoryTask);

                var userData = userTask.Result;
                var characterData = characterTask.Result;
                var inventoryData = inventoryTask.Result;

                // 데이터가 없으면 예외 처리 (방어 코드)
                if (userData == null || characterData == null || characterData.Count == 0)
                {
                    // 로깅: 캐릭터 데이터 없음
                    isAllSuccess = false;
                    continue;
                }

                var user = JsonSerializer.Deserialize<UserCachingModel>(userData);

                // 2. 캐릭터 데이터 파싱 (여기서는 첫 번째 캐릭터를 대표로 사용한다고 가정)
                var characterJson = characterData.Values.FirstOrDefault();
                var character = JsonSerializer.Deserialize<Character>(characterJson);

                // 3. 인벤토리 데이터 파싱 (IsEquipped == true 인 장비만 필터링)
                List<Item> equippedItems = new List<Item>();
                if (inventoryData != null && inventoryData.Count > 0)
                {
                    equippedItems = inventoryData.Values
                        .Select(json => JsonSerializer.Deserialize<Item>(json))
                        .Where(item => item.IsEquipped) // 장착 중인 아이템만 골라냄
                        .ToList();
                }

                // 4. Protobuf 객체(PlayerFieldData) 생성 및 매핑
                Protocol.PlayerFieldData fieldData = new Protocol.PlayerFieldData
                {
                    Uid = uid,
                    Model = (int)character.CharacterInstanceID, // TODO: 실제 캐릭터 모델(종류) ID가 있다면 교체
                    Nickname = user.NickName,          // TODO: 닉네임 조회 로직 필요 (현재 클래스에 없음)
                    Level = character.Level,
                    Exp = character.Exp
                };

                // 5. 장착 중인 무기와 방어구 세팅
                // (ItemCategory Enum에 Weapon, Armor가 있다고 가정)
                var weapon = equippedItems.FirstOrDefault(i => i.Category == ItemCategory.Weapon);
                if (weapon != null)
                {
                    fieldData.Weapon = new Protocol.EquipmentData
                    {
                        ItemSubcategory = (int)weapon.SubCategory, // 또는 weapon.ItemDataID 등 실제 테이블 ID
                        Level = weapon.Level
                    };
                }

                var armor = equippedItems.FirstOrDefault(i => i.Category == ItemCategory.Armor);
                if (armor != null)
                {
                    fieldData.Armor = new Protocol.EquipmentData
                    {
                        ItemSubcategory = (int)armor.InstanceID, // 또는 armor.ItemDataID 등 실제 테이블 ID
                        Level = armor.Level
                    };
                }

                // 6. 직렬화 및 Redis 스냅샷 캐싱
                byte[] protoBytes = fieldData.ToByteArray();

                // 인게임 서버가 단건으로 바로 읽어갈 수 있는 전용 스냅샷 키 생성
                string snapshotKey = $"player_snapshot:{uid}";

                // byte[] 배열을 Redis String 자료형으로 저장 (유효기간을 설정하는 것을 권장)
                bool success = await _redisHelper.SetKeyValueAsync(snapshotKey, protoBytes);

                if (!success)
                {
                    isAllSuccess = false;
                }
            }

            return isAllSuccess;
        }
    }
}