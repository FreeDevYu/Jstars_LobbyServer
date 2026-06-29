using LobbyAPI;
using LobbyAPI.Models;
using LobbyServer.Repositories;
using Google.Protobuf;
using StackExchange.Redis;
using System.Text.Json;
using LobbyServer.Models;
using LobbyServer.Services;

namespace LobbyServer.Helper
{
    public interface IMatchingHelper
    {
        Task<bool> EnqueueMatchingQueue(long uid);
        Task<bool> CancelMatchingQueue(long uid);
        Task<bool> EnqueuePveMatchingQueue(long uid);
        Task<bool> CancelPveMatchingQueue(long uid);
        Task CancelAllMatchingQueues(long uid);
        Task<bool> CreateRoomAsync(List<long> uidList, int teamCount, Protocol.GameModeType gameMode);
        Task<bool> CachingPlayerData(List<long> uidList);
    }

    public class MatchingHelper : IMatchingHelper
    {
        private readonly ILobbyRespository _lobbyRespository;
        private readonly IInventoryHelper _inventoryHelper;
        private readonly ICharacterHelper _characterHelper;
        private readonly IUserHelper _userHelper;
        private readonly IUserRespository _userRepository;
        private readonly IRedisHelper _redisHelper;
        private readonly IMatchLatencyMetrics _matchLatencyMetrics;
        private readonly string _key = "MatchingQueue_ZSET";
        private readonly string _pveKey = "PveMatchingQueue_ZSET";
        private readonly string _fieldServerCurrentKey = "FIELD_SERVER_CURRENT_USER";


        public MatchingHelper(
            IRedisHelper redisHelper,
            ILobbyRespository lobbyRespository,
            IInventoryHelper inventoryHelper,
            ICharacterHelper characterHelper,
            IUserHelper userHelper,
            IUserRespository userRepository,
            IMatchLatencyMetrics matchLatencyMetrics)
        {
            _lobbyRespository = lobbyRespository;
            _inventoryHelper = inventoryHelper;
            _characterHelper = characterHelper;
            _userHelper = userHelper;
            _userRepository = userRepository;
            _redisHelper = redisHelper;
            _matchLatencyMetrics = matchLatencyMetrics;
        }

        public async Task<bool> EnqueueMatchingQueue(long uid)
        {
            await CancelPveMatchingQueue(uid);

            double score = await GetMatchingScoreAsync(uid);
            await _redisHelper.AddZSetAsync(_key, uid, score);
            await _matchLatencyMetrics.RecordEnqueueAsync(uid);
            return true;
        }

        public async Task<bool> EnqueuePveMatchingQueue(long uid)
        {
            await CancelMatchingQueue(uid);

            double score = await GetMatchingScoreAsync(uid);
            await _redisHelper.AddZSetAsync(_pveKey, uid, score);
            await _matchLatencyMetrics.RecordEnqueueAsync(uid);
            return true;
        }

        public async Task<bool> CancelMatchingQueue(long uid)
        {
            bool removed = await _redisHelper.RemoveZSetAsync(_key, uid);
            await _matchLatencyMetrics.CancelPendingAsync(uid);
            return removed;
        }

        public async Task<bool> CancelPveMatchingQueue(long uid)
        {
            bool removed = await _redisHelper.RemoveZSetAsync(_pveKey, uid);
            await _matchLatencyMetrics.CancelPendingAsync(uid);
            return removed;
        }

        public async Task CancelAllMatchingQueues(long uid)
        {
            await CancelMatchingQueue(uid);
            await CancelPveMatchingQueue(uid);
        }

        private async Task<double> GetMatchingScoreAsync(long uid)
        {
            double score = 50.0;
            string redisKey = "PvpRecord";
            PvpRecord? pvpRecord = null;

            string jsonValue = await _redisHelper.GetHashFieldAsync(redisKey, uid.ToString());

            if (string.IsNullOrEmpty(jsonValue))
            {
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

            return score;
        }

        public async Task<bool> CreateRoomAsync(List<long> uidList, int teamCount, Protocol.GameModeType gameMode)
        {
            int gameMapId = 1;

            // 가장 인원이 적은 서버 찾기 (로드밸런싱)
            RedisValue[] serverList = await _redisHelper.GetZSetRangeAsync(
                _fieldServerCurrentKey, start: 0, stop: 0, order: Order.Ascending);

            if (serverList.Length < 1)
                return false;

            string targetServerName = serverList[0].ToString();

            // DTO 생성 및 맵 ID 세팅
            Protocol.GameRoomCreateDTO dto = new Protocol.GameRoomCreateDTO
            {
                GameMapId = gameMapId,
                GameMode = gameMode
            };

            // 팀 분배 및 PlayerInfo 객체 추가
            for (int i = 0; i < uidList.Count; i++)
            {
                // teamCount가 1 이하면(개인전/데스매치) 모두 0, 아니면 1팀, 2팀... 순차 배분
                int assignedTeamId = 0;
                if (gameMode == Protocol.GameModeType.GameModePve)
                {
                    assignedTeamId = 1;
                }
                else if (teamCount > 1)
                {
                    assignedTeamId = i % teamCount + 1;
                }

                dto.Players.Add(new Protocol.PlayerInfo
                {
                    Uid = (ulong)uidList[i],
                    TeamId = assignedTeamId
                });
            }

            byte[] protoBytes = dto.ToByteArray();

            string redisKey = $"MatchingQueue:{targetServerName}";
            long count = await _redisHelper.EnqueueKeyValueAsync(redisKey, protoBytes);

            return count > 0;
        }

        public async Task<bool> CachingPlayerData(List<long> uidList)
        {
            bool isAllSuccess = true;

            foreach (long uid in uidList)
            {
                UserCachingModel? user = await ResolveUserCachingModelAsync(uid);
                if (user == null)
                {
                    isAllSuccess = false;
                    continue;
                }

                List<Character> characters = await _characterHelper.GetAllCharactersByUIDAsync(uid);
                if (characters == null || characters.Count == 0)
                {
                    isAllSuccess = false;
                    continue;
                }

                Character character = characters[0];

                List<Item> inventory = await _inventoryHelper.GetInventoryListByUIDAsync(uid);
                List<Item> equippedItems = inventory
                    .Where(item => item.IsEquipped)
                    .ToList();

                Protocol.PlayerFieldData fieldData = new Protocol.PlayerFieldData
                {
                    Uid = uid,
                    Model = (int)character.CharacterInstanceID,
                    Nickname = user.NickName,
                    Level = character.Level,
                    Exp = character.Exp
                };

                Item? weapon = equippedItems.FirstOrDefault(i => i.Category == ItemCategory.Weapon);
                if (weapon != null)
                {
                    fieldData.Weapon = new Protocol.EquipmentData
                    {
                        ItemSubcategory = (int)weapon.SubCategory,
                        Level = weapon.Level
                    };
                }

                Item? armor = equippedItems.FirstOrDefault(i => i.Category == ItemCategory.Armor);
                if (armor != null)
                {
                    fieldData.Armor = new Protocol.EquipmentData
                    {
                        ItemSubcategory = (int)armor.InstanceID,
                        Level = armor.Level
                    };
                }

                byte[] protoBytes = fieldData.ToByteArray();
                string snapshotKey = $"player_snapshot:{uid}";
                bool success = await _redisHelper.SetKeyValueAsync(snapshotKey, protoBytes);

                if (!success)
                {
                    isAllSuccess = false;
                }
            }

            return isAllSuccess;
        }

        private async Task<UserCachingModel?> ResolveUserCachingModelAsync(long uid)
        {
            string? userData = await _redisHelper.GetValueAsync($"user:{uid}");
            if (!string.IsNullOrEmpty(userData))
            {
                return JsonSerializer.Deserialize<UserCachingModel>(userData);
            }

            User? user = await _userRepository.GetUserByUIDAsync(uid);
            if (user == null)
            {
                return null;
            }

            await _userHelper.SetUserData(user);
            return new UserCachingModel
            {
                UID = user.UID,
                NickName = user.NickName,
                Gold = user.Gold
            };
        }
    }
}
