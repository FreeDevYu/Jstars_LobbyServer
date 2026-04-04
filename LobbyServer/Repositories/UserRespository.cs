using Dapper;
using LobbyServer.Models;
using SqlKata.Execution;
using System.Data;

namespace LobbyServer.Repositories
{
    public interface IUserRespository
    {
        //Task<User> GetByIdAsync(int uid);
        Task<User> GetByUserIDAsync(string id);
        Task<User> GetByEmailAsync(string email);
        Task<int> CreateAsync(User user);
        Task<bool> UpdateAsync(User user);
        Task<bool> UpdateLastLoginAsync(long uid, DateTime loginTime);
    }

    public class UserRespository : IUserRespository
    {
        private readonly QueryFactory _db;

        public UserRespository(QueryFactory db)
        {
            _db = db;
        }

        // public async Task<User> GetByIdAsync(int uid)
        // {
        //     return await _db.Query("users")
        //         .Where("uid", uid)
        //         .FirstOrDefaultAsync<User>();
        // }

        public async Task<User> GetByUserIDAsync(string id)
        {
            return await _db.Query("users")
                            .Where("id", id)
                            .Select(
                                "uid AS Uid",
                                "id AS ID",
                                "email AS Email",
                                "password_hash AS PasswordHash",
                                "salt AS Salt",
                                "created_at AS CreatedAt",
                                "last_login_at AS LastLoginAt",
                                "status AS Status"
                            )
                            .FirstOrDefaultAsync<User>();
        }

        public async Task<User> GetByEmailAsync(string email)
        {
            return await _db.Query("users")
                .Where("email", email)
                .FirstOrDefaultAsync<User>();
        }

        public async Task<int> CreateAsync(User user)
        {
            // 1. 프로시저의 IN 파라미터 이름(p_id, p_email 등)에 맞춰 익명 객체 생성
            var parameters = new
            {
                p_id = user.ID,
                p_email = user.Email,
                p_password_hash = user.PasswordHash,
                p_salt = user.Salt,
                p_nickname = user.NickName
            };

            // 2. 프로시저 호출
            // 프로시저 마지막에 'SELECT @new_uid AS new_uid;' 로 값을 반환하므로 
            // QuerySingleAsync<int>를 사용하여 결과값을 바로 받아옵니다.
            int uid = await _db.Connection.QuerySingleAsync<int>(
                "CreateAccount",
                parameters,
                commandType: CommandType.StoredProcedure
            );

            return uid;

            //  int uid =  await _db.Query("users").InsertGetIdAsync<int>(new
            //  {
            //      //uid = user.UID, -> 자동생성
            //      id = user.ID,
            //      email = user.Email,
            //      password_hash = user.PasswordHash,
            //      salt = user.Salt,
            //      created_at = user.CreatedAt,
            //      last_login_at = user.LastLoginAt,
            //      status = user.Status
            //      // last_login_at은 여기서 DB에 NULL이 들어갑니다 (컬럼이 NULL 허용)
            //  });
            //
            //  return uid;
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
                    status = user.Status
                });

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
