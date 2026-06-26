using LobbyServer.Models;
using ProtoBuf;

namespace LobbyServer.Test;

[ProtoContract]
public class TestEmailAuthRequest
{
    [ProtoMember(1)]
    public string Email { get; set; } = string.Empty;
}

[ProtoContract]
public class TestEmailAuthResponse
{
    [ProtoMember(1)]
    public bool Success { get; set; }

    [ProtoMember(2)]
    public string AuthToken { get; set; } = string.Empty;
}

[ProtoContract]
public class TestRegisterRequest
{
    [ProtoMember(1)]
    public string ID { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string Password { get; set; } = string.Empty;

    [ProtoMember(3)]
    public string Email { get; set; } = string.Empty;
}

[ProtoContract]
public class TestRegisterResponse
{
    [ProtoMember(1)]
    public bool Success { get; set; }

    [ProtoMember(2)]
    public RegistResponse.ResultState State { get; set; }

    [ProtoMember(3)]
    public string AuthToken { get; set; } = string.Empty;

    [ProtoMember(4)]
    public string Detail { get; set; } = string.Empty;
}
