using LobbyAPI;
using LobbyAPI.Models;
using LobbyServer.Helper;
using LobbyServer.Models;
using LobbyServer.Repositories;

namespace LobbyServer.Services
{
    public interface IShopService
    {
        Task<ShopPurchaseResponse> PurchaseProductAsync(ShopPurchaseRequest request);
    }

    public class ShopService : IShopService
    {
        private readonly IShopRespository _shopRespository;
        private readonly IInventoryHelper _inventoryHelper;
        private readonly IUserHelper _userHelper;
        private readonly IRedisHelper _redisHelper;

        public ShopService(
            IShopRespository shopRespository,
            IInventoryHelper inventoryHelper,
            IUserHelper userHelper,
            IRedisHelper redisHelper)
        {
            _shopRespository = shopRespository;
            _inventoryHelper = inventoryHelper;
            _userHelper = userHelper;
            _redisHelper = redisHelper;
        }

        public async Task<ShopPurchaseResponse> PurchaseProductAsync(ShopPurchaseRequest request)
        {
            ShopPurchaseProcedureResult purchaseResult =
                await _shopRespository.PurchaseProductAsync(request.UID, request.ProductId);

            var response = new ShopPurchaseResponse
            {
                Result = (ShopPurchaseResult)purchaseResult.Result,
                RemainGold = purchaseResult.RemainGold,
                RewardItem = new Item()
            };

            if (purchaseResult.Result != (int)ShopPurchaseResult.Success)
                return response;

            if (purchaseResult.RewardInstanceId <= 0)
            {
                response.Result = ShopPurchaseResult.Fail;
                return response;
            }

            Item rewardItem = new Item
            {
                InstanceID = purchaseResult.RewardInstanceId,
                Category = (ItemCategory)purchaseResult.RewardCategory,
                SubCategory = (ItemSubCategory)purchaseResult.RewardSubCategory,
                Level = purchaseResult.RewardLevel,
                Count = purchaseResult.RewardCount,
                IsEquipped = false
            };

            bool cacheUpdated = await _inventoryHelper.AddItemToCacheAsync(request.UID, rewardItem);
            if (!cacheUpdated)
            {
                await _redisHelper.DeleteKeyAsync($"inventory:{request.UID}");
            }

            await _userHelper.UpdateUserGold(request.UID, purchaseResult.RemainGold);

            response.RewardItem = new Item
            {
                InstanceID = purchaseResult.RewardInstanceId
            };

            return response;
        }
    }
}
