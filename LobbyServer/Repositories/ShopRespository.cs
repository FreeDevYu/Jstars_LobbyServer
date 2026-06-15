using Dapper;
using LobbyServer.Models;
using MySqlConnector;
using SqlKata.Execution;
using System.Data;

namespace LobbyServer.Repositories
{
    public interface IShopRespository
    {
        Task<ShopPurchaseProcedureResult> PurchaseProductAsync(long uid, long productId);
    }

    public class ShopRespository : IShopRespository
    {
        private readonly QueryFactory _db;

        public ShopRespository(QueryFactory db)
        {
            _db = db;
        }

        public async Task<ShopPurchaseProcedureResult> PurchaseProductAsync(long uid, long productId)
        {
            var parameters = new DynamicParameters();
            parameters.Add("input_uid", uid);
            parameters.Add("input_product_id", productId);
            parameters.Add("output_result", dbType: DbType.Int32, direction: ParameterDirection.Output);
            parameters.Add("output_remain_gold", dbType: DbType.Int64, direction: ParameterDirection.Output);
            parameters.Add("output_reward_instance_id", dbType: DbType.Int64, direction: ParameterDirection.Output);
            parameters.Add("output_reward_category", dbType: DbType.Int32, direction: ParameterDirection.Output);
            parameters.Add("output_reward_sub_category", dbType: DbType.Int32, direction: ParameterDirection.Output);
            parameters.Add("output_reward_level", dbType: DbType.Int32, direction: ParameterDirection.Output);
            parameters.Add("output_reward_count", dbType: DbType.Int32, direction: ParameterDirection.Output);

            try
            {
                await _db.Connection.ExecuteAsync(
                    "PurchaseProduct",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                return new ShopPurchaseProcedureResult
                {
                    Result = parameters.Get<int>("output_result"),
                    RemainGold = parameters.Get<long>("output_remain_gold"),
                    RewardInstanceId = parameters.Get<long>("output_reward_instance_id"),
                    RewardCategory = parameters.Get<int>("output_reward_category"),
                    RewardSubCategory = parameters.Get<int>("output_reward_sub_category"),
                    RewardLevel = parameters.Get<int>("output_reward_level"),
                    RewardCount = parameters.Get<int>("output_reward_count")
                };
            }
            catch (MySqlException)
            {
                return new ShopPurchaseProcedureResult();
            }
        }
    }
}
