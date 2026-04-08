using Dapper;
using LobbyAPI.Models;
using MySqlConnector;
using SqlKata.Execution;
using System.Data;

namespace LobbyServer.Repositories
{
    public interface ILobbyRespository
    {
        Task<IEnumerable<Character>> GetAllCharactersByUIDAsync(long uid);
        Task<IEnumerable<Item>> GetInventoryListByUIDAsync(long uid);
        Task<bool> EquipAsync(long uid, long itemID);
        Task<NicknameChangeResult> NicknameChangeAsync(long uid, string newNickname, ItemSubCategory subcategory, long itemInstanceID);
    }

    public class LobbyRespository : ILobbyRespository
    {
        private readonly QueryFactory _db;

        public LobbyRespository(QueryFactory db)
        {
            _db = db;
        }

        public async Task<IEnumerable<Character>> GetAllCharactersByUIDAsync(long uid)
        {
            return await _db.Query("characters")
                    .Where("uid", uid)
                    .Select(
                        "character_instance_id AS CharacterInstanceID",
                        "level AS Level",
                        "exp AS Exp"
                    )
                    .GetAsync<Character>();
        }

        public async Task<IEnumerable<Item>> GetInventoryListByUIDAsync(long uid)
        {
            return await _db.Query("inventory")
                  .Where("uid", uid)
                  .Select(
                      "instance_id AS InstanceID",
                      "category AS Category",
                      "sub_category AS SubCategory",
                      "level AS Level",
                      "count AS Count",
                      "is_equipped AS IsEquipped"
                  )
                  .GetAsync<Item>();
        }

        public async Task<bool> EquipAsync(long uid, long itemID)
        {
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("input_uid", uid);
                parameters.Add("input_equip_instance_id", itemID);
                parameters.Add("success", dbType: DbType.Byte, direction: ParameterDirection.Output);

                await _db.Connection.ExecuteAsync(
                    "EquipItem",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                byte isSuccess = parameters.Get<byte>("success");
                return isSuccess == 1;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<NicknameChangeResult> NicknameChangeAsync(long uid, string newNickname, ItemSubCategory subcategory, long itemInstanceID)
        {
            // 1. 익명 객체 대신 DynamicParameters를 사용해야 OUT 파라미터를 받을 수 있습니다.
            var parameters = new DynamicParameters();

            // IN 파라미터 세팅
            parameters.Add("input_uid", uid);
            parameters.Add("input_new_nickname", newNickname);
            parameters.Add("input_subcategory", subcategory);
            parameters.Add("input_instance_id", itemInstanceID);

            // 2. 핵심: OUT 파라미터를 받을 "빈 공간"을 세팅해서 같이 넘겨줍니다.
            // SQL에서 INT로 선언했으므로 DbType.Int32를 사용합니다.
            parameters.Add("output_result", dbType: DbType.Int32, direction: ParameterDirection.Output);

            try
            {
                // 3. 데이터를 반환하는 SELECT 쿼리가 아니므로 ExecuteAsync를 사용합니다.
                await _db.Connection.ExecuteAsync(
                    "ChangeNickname",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                // 4. 프로시저 실행이 끝난 후, OUT 파라미터 방에 담긴 값을 꺼내옵니다.
                int result = parameters.Get<int>("output_result");

                return (NicknameChangeResult)result; // (0: 실패, 1: 성공, 2: 형식오류, 3: 중복, 4: 티켓없음)
            }
            catch (MySqlException ex)
            {
                // 시스템/네트워크 에러 처리
                // _logger.LogError(ex, "닉네임 변경 프로시저 에러");
                return 0;
            }
        }

    }
}
