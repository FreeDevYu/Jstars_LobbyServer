using Dapper;
using LobbyAPI.Models;
using LobbyServer.Models;
using MySqlConnector;
using SqlKata.Execution;
using System.Data;

namespace LobbyServer.Repositories
{
    public interface ILobbyRespository
    {
        Task<PvpRecord?> GetPvpRecordByUIDAsync(long uid);
        Task<IEnumerable<Character>> GetAllCharactersByUIDAsync(long uid);
        Task<IEnumerable<Item>> GetInventoryListByUIDAsync(long uid);
        Task<bool> EquipAsync(long uid, long itemID);
        Task<bool> UpdateCharacterLevelExpAsync(long characterInstanceId, int level, long exp);
        Task<PvpRecord?> IncrementPvpRecordAsync(long uid, bool isWin);
        Task<NicknameChangeResult> NicknameChangeAsync(long uid, string newNickname, ItemSubCategory subcategory, long itemInstanceID);
        Task<AddItemResult?> AddItemAsync(long uid, ItemCategory category, ItemSubCategory subCategory, int level, int count);
        Task<IReadOnlyList<RankingEntry>> GetPvpRankingTopAsync();
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

        public async Task<PvpRecord?> GetPvpRecordByUIDAsync(long uid)
        {
            return await _db.Query("match_records")
                 .Where("uid", uid)
                 .Select(
                     "uid AS UID",
                     "pvp_win_count AS Win",
                     "pvp_play_count AS Total"
                 )
                 .FirstOrDefaultAsync<PvpRecord>();
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

        public async Task<bool> UpdateCharacterLevelExpAsync(long characterInstanceId, int level, long exp)
        {
            var affected = await _db.Query("characters")
                .Where("character_instance_id", characterInstanceId)
                .UpdateAsync(new
                {
                    level = level,
                    exp = exp
                });

            return affected > 0;
        }

        //단순 카운터 (win/total) → SQL 원자적 증가
        public async Task<PvpRecord?> IncrementPvpRecordAsync(long uid, bool isWin)
        {
            int winIncrement = isWin ? 1 : 0;

            int affected = await _db.Connection.ExecuteAsync(@"
                UPDATE match_records
                SET pvp_play_count = pvp_play_count + 1,
                    pvp_win_count = pvp_win_count + @winIncrement
                WHERE uid = @uid",
                new { uid, winIncrement });

            if (affected == 0)
            {
                await _db.Connection.ExecuteAsync(@"
                    INSERT INTO match_records (uid, pvp_win_count, pvp_play_count)
                    VALUES (@uid, @winIncrement, 1)",
                    new { uid, winIncrement });
            }

            return await GetPvpRecordByUIDAsync(uid);
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

        public async Task<AddItemResult?> AddItemAsync(
            long uid,
            ItemCategory category,
            ItemSubCategory subCategory,
            int level,
            int count)
        {
            if (uid <= 0 || count <= 0)
                return null;

            var parameters = new DynamicParameters();
            parameters.Add("input_uid", uid);
            parameters.Add("input_category", (int)category);
            parameters.Add("input_sub_category", (int)subCategory);
            parameters.Add("input_level", level);
            parameters.Add("input_count", count);
            parameters.Add("output_success", dbType: DbType.Byte, direction: ParameterDirection.Output);
            parameters.Add("output_new_instance_id", dbType: DbType.Int64, direction: ParameterDirection.Output);

            try
            {
                await _db.Connection.ExecuteAsync(
                    "AddItem",
                    parameters,
                    commandType: CommandType.StoredProcedure);

                byte success = parameters.Get<byte>("output_success");
                long instanceId = parameters.Get<long>("output_new_instance_id");

                return new AddItemResult
                {
                    Success = success == 1 && instanceId > 0,
                    InstanceId = instanceId
                };
            }
            catch (MySqlException)
            {
                return null;
            }
        }

        public async Task<IReadOnlyList<RankingEntry>> GetPvpRankingTopAsync()
        {
            const string sql = @"
                SELECT
                    ranked.display_rank AS DisplayRank,
                    ranked.uid          AS UID,
                    ranked.nickname     AS Nickname,
                    ranked.win          AS Win,
                    ranked.total        AS Total,
                    ranked.win_rate     AS WinRate
                FROM (
                    SELECT
                        mr.uid,
                        u.nickname,
                        mr.pvp_win_count  AS win,
                        mr.pvp_play_count AS total,
                        ROUND(mr.pvp_win_count * 100.0 / mr.pvp_play_count, 2) AS win_rate,
                        RANK() OVER (
                            ORDER BY
                                ROUND(mr.pvp_win_count * 100.0 / mr.pvp_play_count, 2) DESC,
                                mr.pvp_play_count DESC
                        ) AS display_rank
                    FROM match_records mr
                    JOIN users u ON u.uid = mr.uid
                    WHERE mr.pvp_play_count >= @minPlayCount
                ) ranked
                ORDER BY
                    ranked.display_rank ASC,
                    ranked.uid ASC
                LIMIT @topEntryCount;";

            IEnumerable<RankingEntry> entries = await _db.Connection.QueryAsync<RankingEntry>(
                sql,
                new
                {
                    minPlayCount = RankingConstants.MinPlayCount,
                    topEntryCount = RankingConstants.TopEntryCount
                });

            return entries.ToList();
        }
    }
}
