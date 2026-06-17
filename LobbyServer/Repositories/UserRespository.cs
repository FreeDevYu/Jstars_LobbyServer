using Dapper;
using LobbyAPI.Models;
using LobbyServer.Models;
using MySqlConnector;
using SqlKata.Execution;
using System.Data;


namespace LobbyServer.Repositories
{
    public interface IUserRespository
    {
        Task<User> GetUserByUIDAsync(long uid);
        Task<User> GetUserByIDAsync(string id);
        Task<User> GetByEmailAsync(string email);
        Task<long> CreateIDAsync(User user);
        Task<bool> UpdateAsync(User user);
        Task<bool> SetGoldAsync(long uid, long gold);
        Task<bool> UpdateLastLoginAsync(long uid, DateTime loginTime);
    }

    public class UserRespository : IUserRespository
    {
        private readonly QueryFactory _db;

        public UserRespository(QueryFactory db)
        {
            _db = db;
        }

        public async Task<User> GetUserByIDAsync(string id)
        {
            return await _db.Query("users")
                            .Where("id", id)
                            .Select(
                                "uid AS UID",
                                "id AS ID",
                                "email AS Email",
                                "password_hash AS PasswordHash",
                                "salt AS Salt",
                                "created_at AS CreatedAt",
                                "last_login_at AS LastLoginAt",
                                "status AS Status",
                                "nickname AS NickName",
                                "gold AS Gold"
                            )
                            .FirstOrDefaultAsync<User>();
        }

        public async Task<User> GetUserByUIDAsync(long uid)
        {
            return await _db.Query("users")
                            .Where("uid", uid)
                            .Select(
                                "uid AS UID",
                                "id AS ID",
                                "email AS Email",
                                "password_hash AS PasswordHash",
                                "salt AS Salt",
                                "created_at AS CreatedAt",
                                "last_login_at AS LastLoginAt",
                                "status AS Status",
                                "nickname AS NickName",
                                "gold AS Gold"
                            )
                            .FirstOrDefaultAsync<User>();
        }

        public async Task<User> GetByEmailAsync(string email)
        {
            return await _db.Query("users")
                .Where("email", email)
                .FirstOrDefaultAsync<User>();
        }

        public async Task<long> CreateIDAsync(User user)
        {
            // 1. 프로시저의 IN 파라미터 이름(p_id, p_email 등)에 맞춰 익명 객체 생성
            var parameters = new
            {
                input_id = user.ID,
                input_email = user.Email,
                input_password_hash = user.PasswordHash,
                input_salt = user.Salt,
            };

            try
            {
                // 1. 정상적으로 생성이 완료되면 생성된 uid를 반환합니다.
                long uid = await _db.Connection.QuerySingleAsync<long>(
                    "CreateAccount",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                return uid;
            }
            catch (MySqlException ex)
            {
                // 2. MySQL 에러 번호 1062는 'Unique 제약조건 위배(중복)'를 의미합니다.
                if (ex.Number == 1062)
                {
                    // 중복된 ID (또는 이메일)로 인해 DB가 저장을 거부함
                    // 호출한 쪽에서 "아, 중복이구나"라고 알 수 있도록 에러 코드(예: -1)를 반환합니다.
                    return -1;
                }

                // 3. 중복이 아닌 다른 치명적인 DB 에러 (네트워크 끊김, 쿼리 문법 에러 등)
                // 이런 에러는 보통 서버 로그에 상세히 기록(로깅)하고 예외를 다시 던집니다.
                // _logger.LogError(ex, "DB 계정 생성 중 예기치 못한 에러 발생");
                return -1;
            }
        }

        public async Task<bool> UpdateAsync(User user)
        {
            var affected = await _db.Query("users")
                .Where("uid", user.UID)
                .UpdateAsync(new
                {
                    email = user.Email,
                    password_hash = user.PasswordHash,
                    salt = user.Salt,
                    last_login_at = user.LastLoginAt,
                    status = user.Status,
                    gold = user.Gold,
                });

            return affected > 0;
        }

        public async Task<bool> SetGoldAsync(long uid, long gold)
        {
            if (uid <= 0 || gold < 0)
                return false;

            int affected = await _db.Query("users")
                .Where("uid", uid)
                .UpdateAsync(new { gold });

            return affected > 0;
        }

        public async Task<bool> UpdateLastLoginAsync(long uid, DateTime loginTime)
        {
            var affected = await _db.Query("users")
                .Where("uid", uid)
                .UpdateAsync(new { last_login_at = loginTime });

            return affected > 0;
        }
    }
}
