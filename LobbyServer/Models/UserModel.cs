namespace LobbyServer.Models
{
    public class User
    {
        public long UID { get; set; }
        public string ID { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string Status { get; set; } = string.Empty;//'active', 'suspended', 'banned'
        public string NickName { get; set; } = string.Empty;
    }

    // 클라이언트에 전송할 안전한 사용자 모델 (패스워드 정보 제외)
    public class UserSafeModel
    {
        public long UID { get; set; }
        public string ID { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string NickName { get; set; } = string.Empty;

        public static UserSafeModel FromUser(User user)
        {
            return new UserSafeModel
            {
                UID = user.UID,
                ID = user.ID,
                Email = user.Email,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                Status = user.Status,
                NickName = user.NickName
            };
        }
    }
}