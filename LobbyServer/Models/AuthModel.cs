using ProtoBuf;

namespace LobbyServer.Models
{
    [ProtoContract]
    public class UsingIDRequest
    {
        [ProtoMember(1)]
        public string ID { get; set; } = null!;
    }

    [ProtoContract]
    public class UsingIDResponse
    {
        [ProtoMember(1)]
        public bool Success { get; set; }
    }

    [ProtoContract]
    public class EmailAuthRequest
    {
        [ProtoMember(1)]
        public string Email { get; set; } = null!;
    }

    [ProtoContract]
    public class EmailAuthResponse
    {
        [ProtoMember(1)]
        public bool Success { get; set; }
    }

    [ProtoContract]
    public class RegistRequest
    {
        [ProtoMember(1)]
        public string ID { get; set; } = null!;
        [ProtoMember(2)]
        public string Password { get; set; } = null!;
        [ProtoMember(3)]
        public string Email { get; set; } = null!;
        [ProtoMember(4)]
        public string EmailAuthToken { get; set; } = null!;
    }

    [ProtoContract]
    public class RegistResponse
    {
        [ProtoContract]
        public enum ResultState
        {
            Success,
            UsingID,
            UsingEmail,
            InvalidPassword,
            InvalidEmailAuthToken,
            Unknown
        }

        [ProtoMember(1)]
        public ResultState State { get; set; }
        [ProtoMember(2)]
        public bool EmailTokenDeleted { get; set; } = false;
    }

    [ProtoContract]
    public class LoginRequest
    {
        [ProtoMember(1)]
        public string ID { get; set; } = null!;
        [ProtoMember(2)]
        public string Password { get; set; } = null!;
        [ProtoMember(3)]
        public string DeviceID { get; set; } = null!;
    }

    [ProtoContract]
    public class LoginResponse
    {
        [ProtoContract]
        public enum ResultState
        {
            Success,
            InvalidID,
            InvalidPassowrd,
            InvalidState,
            InvalidToken,
            Unknown
        }

        [ProtoMember(1)]
        public ResultState State { get; set; }
        [ProtoMember(2)]
        public UserSafeModel User { get; set; }
        [ProtoMember(3)]
        public string? Token { get; set; } // 로그인 성공 시 발급될 JWT
    }

    [ProtoContract]
    public class LogoutRequest
    {
        [ProtoMember(1)]
        public string ID { get; set; }
    }

    [ProtoContract]
    public class LogoutResponse
    {
        [ProtoMember(1)]
        public bool Success { get; set; }
    }

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
        public long Gold { get; set; } = 0;
    }

    // 클라이언트에 전송할 안전한 사용자 모델 (패스워드 정보 제외)
    [ProtoContract]
    public class UserSafeModel
    {
        [ProtoMember(1)]
        public long UID { get; set; }
        [ProtoMember(2)]
        public string ID { get; set; } = string.Empty;
        [ProtoMember(3)]
        public string Email { get; set; } = string.Empty;
        [ProtoMember(4)]
        public DateTime CreatedAt { get; set; }
        [ProtoMember(5)]
        public DateTime? LastLoginAt { get; set; }
        [ProtoMember(6)]
        public string Status { get; set; } = string.Empty;
        [ProtoMember(7)]
        public string NickName { get; set; } = string.Empty;
        [ProtoMember(8)]
        public long Gold { get; set; } = 0;

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
                NickName = user.NickName,
                Gold = user.Gold,
            };
        }
    }

    [ProtoContract]
    public class UserCachingModel
    {
        [ProtoMember(1)]
        public long UID { get; set; }
        [ProtoMember(2)]
        public string NickName { get; set; } = string.Empty;
        [ProtoMember(3)]
        public long Gold { get; set; }
    }
}