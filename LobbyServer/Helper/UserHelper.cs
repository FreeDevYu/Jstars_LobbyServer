using LobbyAPI;
using LobbyAPI.Models;
using LobbyServer.Models;
using LobbyServer.Repositories;
using System.Text.Json;

namespace LobbyServer.Helper
{
    public interface IUserHelper
    {
        Task<bool> SetUserData(User user);
        Task<bool> UpdateUserNickname(long uid, string newNIckname);
        Task<bool> UpdateUserGold(long uid, long gold);
        Task<bool> ApplyNewAccountRewardsAsync(long uid);
        Task<bool> GrantGoldAsync(long uid, long goldAmount);
        Task<bool> GrantItemAsync(long uid, NewAccountItemReward reward);
    }

    public class UserHelper : IUserHelper
    {
        private readonly IRedisHelper _redisHelper;
        private readonly IUserRespository _userRepository;
        private readonly ILobbyRespository _lobbyRepository;
        private readonly IInventoryHelper _inventoryHelper;
        private readonly TimeSpan _expiry = TimeSpan.FromHours(2);

        public UserHelper(
            IRedisHelper redisHelper,
            IUserRespository userRepository,
            ILobbyRespository lobbyRepository,
            IInventoryHelper inventoryHelper)
        {
            _redisHelper = redisHelper;
            _userRepository = userRepository;
            _lobbyRepository = lobbyRepository;
            _inventoryHelper = inventoryHelper;
        }

        public async Task<bool> SetUserData(User user)
        {
            if (user == null)
                return false;

            UserCachingModel cachingModel = new UserCachingModel();
            cachingModel.UID = user.UID;
            cachingModel.NickName = user.NickName;
            cachingModel.Gold = user.Gold;

            string redisKey = $"user:{cachingModel.UID}";
            string jsonValue = JsonSerializer.Serialize(cachingModel);

            bool success = await _redisHelper.SetKeyValueAsync(redisKey, jsonValue, _expiry);

            return success;
        }

        public async Task<bool> UpdateUserNickname(long uid, string newNIckname)
        {
            if (uid <= 0 || string.IsNullOrWhiteSpace(newNIckname))
                return false;

            string redisKey = $"user:{uid}";
            string jsonValue = await _redisHelper.GetValueAsync(redisKey);

            UserCachingModel cachingModel;
            if (string.IsNullOrEmpty(jsonValue))
            {
                cachingModel = new UserCachingModel
                {
                    UID = uid,
                    NickName = newNIckname
                };
            }
            else
            {
                cachingModel = JsonSerializer.Deserialize<UserCachingModel>(jsonValue);
                if (cachingModel == null)
                    return false;

                cachingModel.NickName = newNIckname;
            }

            string updatedJson = JsonSerializer.Serialize(cachingModel);
            return await _redisHelper.SetKeyValueAsync(redisKey, updatedJson, _expiry);
        }

        public async Task<bool> UpdateUserGold(long uid, long gold)
        {
            if (uid <= 0 || gold < 0)
                return false;

            string redisKey = $"user:{uid}";
            string jsonValue = await _redisHelper.GetValueAsync(redisKey);

            UserCachingModel cachingModel;
            if (string.IsNullOrEmpty(jsonValue))
            {
                cachingModel = new UserCachingModel
                {
                    UID = uid,
                    Gold = gold
                };
            }
            else
            {
                cachingModel = JsonSerializer.Deserialize<UserCachingModel>(jsonValue);
                if (cachingModel == null)
                    return false;

                cachingModel.Gold = gold;
            }

            string updatedJson = JsonSerializer.Serialize(cachingModel);
            return await _redisHelper.SetKeyValueAsync(redisKey, updatedJson, _expiry);
        }

        public async Task<bool> ApplyNewAccountRewardsAsync(long uid)
        {
            if (uid <= 0)
                return false;

            if (NewAccountRewardConfig.StarterGold > 0)
            {
                if (!await GrantGoldAsync(uid, NewAccountRewardConfig.StarterGold))
                    return false;
            }

            foreach (NewAccountItemReward itemReward in NewAccountRewardConfig.StarterItems)
            {
                if (!await GrantItemAsync(uid, itemReward))
                    return false;
            }

            await _inventoryHelper.SyncInventoryCacheFromDbAsync(uid);
            return true;
        }

        public async Task<bool> GrantGoldAsync(long uid, long goldAmount)
        {
            if (uid <= 0 || goldAmount < 0)
                return false;

            bool dbUpdated = await _userRepository.SetGoldAsync(uid, goldAmount);
            if (!dbUpdated)
                return false;

            return await UpdateUserGold(uid, goldAmount);
        }

        public async Task<bool> GrantItemAsync(long uid, NewAccountItemReward reward)
        {
            if (uid <= 0 || reward == null || reward.Count <= 0)
                return false;

            AddItemResult? addResult = await _lobbyRepository.AddItemAsync(
                uid,
                reward.Category,
                reward.SubCategory,
                reward.Level,
                reward.Count);

            if (addResult == null || !addResult.Success)
                return false;

            Item grantedItem = new Item
            {
                InstanceID = addResult.InstanceId,
                Category = reward.Category,
                SubCategory = reward.SubCategory,
                Level = reward.Level,
                Count = reward.Count,
                IsEquipped = false
            };

            bool cacheUpdated = await _inventoryHelper.AddItemToCacheAsync(uid, grantedItem);
            if (!cacheUpdated)
            {
                await _redisHelper.DeleteKeyAsync($"inventory:{uid}");
                return false;
            }

            if (!reward.EquipOnGrant)
                return true;

            (bool equipResult, _, _) = await _inventoryHelper.EquipItem(uid, grantedItem.InstanceID);
            return equipResult;
        }
    }
}
