using ProtoBuf;

namespace FieldStressHarness.Protocol;

[ProtoContract]
public enum LoginResultState
{
    Success = 0,
    InvalidID = 1,
    InvalidPassowrd = 2,
    InvalidState = 3,
    InvalidToken = 4,
    Unknown = 5
}

[ProtoContract]
public class LoginRequest
{
    [ProtoMember(1)]
    public string ID { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Password { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string DeviceID { get; set; } = string.Empty;
}

[ProtoContract]
public class LoginResponse
{
    [ProtoMember(1)]
    public LoginResultState State { get; set; }

    [ProtoMember(2)]
    public UserSafeModel? User { get; set; }

    [ProtoMember(3)]
    public string? Token { get; set; }
}

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
    public long Gold { get; set; }
}

[ProtoContract]
public class EnqueueMatchingRequest
{
    [ProtoMember(1)]
    public long UID { get; set; }
}

[ProtoContract]
public class EnqueueMatchingResponse
{
    [ProtoMember(1)]
    public bool Success { get; set; }
}

[ProtoContract]
public class CancelMatchingRequest
{
    [ProtoMember(1)]
    public long UID { get; set; }
}

[ProtoContract]
public class CancelMatchingResponse
{
    [ProtoMember(1)]
    public bool Success { get; set; }
}
