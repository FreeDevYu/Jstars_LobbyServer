using LobbyAPI;
using LobbyAPI.Models;
using LobbyServer.Repositories;
using System.Text.Json;


namespace LobbyServer.Helper
{
    public interface IInventoryHelper
    {
        Task<List<Item>> GetInventoryListByUIDAsync(long uid);
        Task<(bool result, long equipped, long unequipped)> EquipItem(long uid, long itemID);
        Task<NicknameChangeResponse> ChangeNickname(long uid, string newNickname, long itemInstanceID);
        Task<bool> AddItemToCacheAsync(long uid, Item item);
    }

    public class InventoryHelper : IInventoryHelper
    {
        private readonly ILobbyRespository _lobbyRespository;
        private readonly IRedisHelper _redisHelper;
        private readonly TimeSpan _inventoryDataExpiry = TimeSpan.FromHours(2);

        public InventoryHelper(IRedisHelper redisHelper, ILobbyRespository lobbyRespository)
        {
            _lobbyRespository = lobbyRespository;
            _redisHelper = redisHelper;
        }

        public async Task<List<Item>> GetInventoryListByUIDAsync(long uid)
        {
            List<Item> result = new List<Item>();
            string redisKey = $"inventory:{uid}";

            var cachingData = await _redisHelper.GetAllHashFieldsAsync(redisKey);

            if (cachingData != null && cachingData.Count > 0)
            {
                result = cachingData.Values
                            .Select(json => JsonSerializer.Deserialize<Item>(json))
                            .ToList();
                return result;
            }

            var dbData = await _lobbyRespository.GetInventoryListByUIDAsync(uid);
            result = dbData.ToList();

            if (result.Count > 0)
            {
                var hashEntries = new Dictionary<string, string>();

                foreach (var item in result)
                {
                    string field = item.InstanceID.ToString();
                    string jsonValue = JsonSerializer.Serialize(item);
                    hashEntries.Add(field, jsonValue);
                }

                await _redisHelper.SetHashFieldsAsync(redisKey, hashEntries, _inventoryDataExpiry);
            }

            return result;
        }

        public async Task<(bool result, long equipped, long unequipped)> EquipItem(long uid, long itemID)
        {
            string redisKey = $"inventory:{uid}";
            string lockKey = $"lock:inventory:{uid}";
            
            // 분산 락 획득 (예시: 3초 동안 락 점유, 유저당 동시 접근 방지)
            string lockToken = Guid.NewGuid().ToString();
            bool isLocked = await _redisHelper.AcquireLockAsync(lockKey, lockToken, TimeSpan.FromSeconds(3));
            if (!isLocked) return (false, -1, -1); // 중복 요청 방어

            List<Item> inventory = await GetInventoryListByUIDAsync(uid);
            if (inventory.Count < 1)
                return (false, -1, -1);

            // 장착 로직 실행
            try
            { 
                Item itemToEquip = inventory.FirstOrDefault(x => x.InstanceID == itemID);
                if (itemToEquip == null || itemToEquip.Category == ItemCategory.Using || itemToEquip.Count < 1 || itemToEquip.IsEquipped) return (false, -1, -1);

                Item itemToUnequip = inventory.FirstOrDefault(x =>
                    x.Category == itemToEquip.Category &&
                    x.IsEquipped &&
                    x.InstanceID != itemID); // 핵심: 방금 장착할 아이템은 제외

                // 상태 변경
                itemToEquip.IsEquipped = true;
                if (itemToUnequip != null) itemToUnequip.IsEquipped = false;

                // 3. Redis 부분 업데이트 (최적화)
                var changedEntries = new Dictionary<string, string>
                {
                     // 변경된 아이템(1~2개)만 직렬화하여 HSET으로 업데이트
                     { itemToEquip.InstanceID.ToString(), JsonSerializer.Serialize(itemToEquip) }
                };

                if (itemToUnequip != null)
                {
                    changedEntries.Add(itemToUnequip.InstanceID.ToString(), JsonSerializer.Serialize(itemToUnequip));
                }

                // 전체 덮어쓰기가 아닌 부분 업데이트(HSET) 수행
                await _redisHelper.SetHashFieldsAsync(redisKey, changedEntries);
                // 주의: _inventoryDataExpiry 갱신이 필요하다면 ExpireAsync를 별도로 호출

                // 4. TODO: DB 최신화 큐에 삽입 (Item 상태 변경 내역 전달)
                await _lobbyRespository.EquipAsync(uid, itemToEquip.InstanceID);

                return (true, itemToEquip.InstanceID, itemToUnequip != null ? itemToUnequip.InstanceID : -1);
            }
            finally
            {
                await _redisHelper.ReleaseLockAsync(lockKey, lockToken);
            }
        }

        public async Task<NicknameChangeResponse> ChangeNickname(long uid, string newNickname, long itemInstanceID)
        {
            NicknameChangeResponse response = new NicknameChangeResponse();

            string redisKey = $"inventory:{uid}";
            string lockKey = $"lock:inventory:{uid}";

            // 분산 락 획득 (예시: 3초 동안 락 점유, 유저당 동시 접근 방지)
            string lockToken = Guid.NewGuid().ToString();
            bool isLocked = await _redisHelper.AcquireLockAsync(lockKey, lockToken, TimeSpan.FromSeconds(3));
            if (!isLocked)
            {
                response.Result = NicknameChangeResult.None;
                return response; // 중복 요청 방어
            }

            try
            {
                List<Item> inventory = await GetInventoryListByUIDAsync(uid);
                if (inventory.Count < 1)
                {
                    response.Result = NicknameChangeResult.NoCoupon;
                    return response;
                }

                ItemSubCategory subCategory = ItemSubCategory.NicknameChangeCoupon;

                Item nicknameCoupon = inventory.FirstOrDefault(x => x.InstanceID == itemInstanceID);

                if (nicknameCoupon == null)
                {
                    response.Result = NicknameChangeResult.NoCoupon;
                    return response;
                }
                if (nicknameCoupon.SubCategory != ItemSubCategory.NicknameChangeCoupon)
                {
                    response.Result = NicknameChangeResult.None;
                    return response;
                }

                if (nicknameCoupon.Count < 1)
                {
                    response.Result = NicknameChangeResult.NoCoupon;
                    return response;
                }

                NicknameChangeResult result = await _lobbyRespository.NicknameChangeAsync(uid, newNickname, subCategory, itemInstanceID);

                if (result == NicknameChangeResult.Success)
                {
                    nicknameCoupon.Count--;

                    response.Result = NicknameChangeResult.Success;
                    response.UID = uid;
                    response.RemainCount = nicknameCoupon.Count;
                    response.ResultNickname = newNickname;
                    if (nicknameCoupon.Count < 1)
                    {
                        await _redisHelper.DeleteHashFieldAsync(redisKey, nicknameCoupon.InstanceID.ToString());
                    }
                    else
                    {
                        var changedEntries = new Dictionary<string, string>
                        {
                            // 변경된 아이템만 직렬화하여 HSET으로 업데이트
                            { nicknameCoupon.InstanceID.ToString(), JsonSerializer.Serialize(nicknameCoupon) }
                        };
                        await _redisHelper.SetHashFieldsAsync(redisKey, changedEntries);
                    }
                }

                return response;
            }
            finally
            {
                await _redisHelper.ReleaseLockAsync(lockKey, lockToken);
            }
        }

        public async Task<bool> AddItemToCacheAsync(long uid, Item item)
        {
            if (item == null || item.InstanceID <= 0)
                return false;

            string redisKey = $"inventory:{uid}";
            string lockKey = $"lock:inventory:{uid}";
            string lockToken = Guid.NewGuid().ToString();
            bool isLocked = await _redisHelper.AcquireLockAsync(lockKey, lockToken, TimeSpan.FromSeconds(3));
            if (!isLocked)
                return false;

            try
            {
                var changedEntries = new Dictionary<string, string>
                {
                    { item.InstanceID.ToString(), JsonSerializer.Serialize(item) }
                };

                await _redisHelper.SetHashFieldsAsync(redisKey, changedEntries, _inventoryDataExpiry);
                return true;
            }
            finally
            {
                await _redisHelper.ReleaseLockAsync(lockKey, lockToken);
            }
        }
    }
}
/*
 * 단순 장착/해제는 상태 데이터이므로 최종본만 덮어쓰기(Overwrite) 하면 되지만,
 * 만약 유저의 행동이 아이템 획득, 소비, 강화, 파괴 등 재화의 본질적인 변화를 일으키는 것이라면 이야기가 다릅니다.

이러한 행위들은 고객 센터 CS 처리나 어뷰징 조사를 위해 반드시 **'로그(Log)'**로 남겨야 합니다.
따라서 장착/해제 상태는 위에서 설명한 대로 30초마다 1번 최종본만 DB에 덮어쓰되,
재화가 변동되는 중요 이벤트는 발생하는 즉시 DB의 로그 테이블에 (비동기 메시지 큐 등을 통해)
10번 모두 INSERT 하도록 시스템을 분리해서 설계하는 것이 정석입니다.
 */