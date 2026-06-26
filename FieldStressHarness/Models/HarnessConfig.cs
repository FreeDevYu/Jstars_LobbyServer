using System.Text.Json.Serialization;

namespace FieldStressHarness.Models;

/// <summary>
/// Harness 설정 (실행 모드는 시작 시 --mode 로 지정).
/// </summary>
public sealed class HarnessConfig
{
    [JsonPropertyName("lobbyBaseUrl")]
    public string LobbyBaseUrl { get; set; } = "http://localhost:5242/api";

    [JsonPropertyName("accountsFile")]
    public string AccountsFile { get; set; } = "accounts.json";

    [JsonPropertyName("register")]
    public RegisterSettings Register { get; set; } = new();

    [JsonPropertyName("match")]
    public MatchSettings Match { get; set; } = new();

    [JsonPropertyName("field")]
    public FieldSettings Field { get; set; } = new();

    public void Normalize()
    {
        Field.Gameplay = Field.Gameplay.Trim().ToLowerInvariant() switch
        {
            "stress" => "idle",
            "combat_baisc" => "combat_basic",
            _ => Field.Gameplay
        };

        Register.ValidateEmail();
    }

    public static string NormalizeMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ArgumentException("mode is required (register | login | full).");
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "register" => "register",
            "login" or "login-only" => "login",
            "full" => "full",
            _ => throw new ArgumentException($"Unknown mode: {raw}. Use register, login, or full.")
        };
    }

    public bool IsRegisterMode(string mode) =>
        string.Equals(mode, "register", StringComparison.OrdinalIgnoreCase);

    public bool IsLoginMode(string mode) =>
        string.Equals(mode, "login", StringComparison.OrdinalIgnoreCase);

    public bool IsPveMatch =>
        string.Equals(Match.Type, "pve", StringComparison.OrdinalIgnoreCase);

    public bool IsCombatBasicMode =>
        string.Equals(Field.Gameplay, "combat_basic", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public int LoginParallelism => Match.Parallelism;

    [JsonIgnore]
    public int MatchTimeoutSeconds => Match.TimeoutSeconds;

    [JsonIgnore]
    public int FieldSessionSeconds => Field.SessionSeconds;

    [JsonIgnore]
    public int HeartbeatIntervalMs => Field.HeartbeatIntervalMs;

    [JsonIgnore]
    public int CombatBasicStepIntervalMs => Field.CombatStepIntervalMs;

    [JsonIgnore]
    public bool SendMovePackets => Field.MoveEnabled;

    [JsonIgnore]
    public int MovePacketIntervalMs => Field.MoveIntervalMs;

    [JsonIgnore]
    public int RegisterCount => Register.Count;

    [JsonIgnore]
    public string RegisterIdPrefix => Register.IdPrefix;

    [JsonIgnore]
    public string RegisterEmail => Register.Email;

    [JsonIgnore]
    public string RegisterPassword => Register.Password;

    [JsonIgnore]
    public bool RegisterWriteAccountsFile => Register.SaveToAccountsFile;

    [JsonIgnore]
    public bool RegisterAppendAccountsFile => Register.AppendToAccountsFile;
}

public sealed class RegisterSettings
{
    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;

    [JsonPropertyName("idPrefix")]
    public string IdPrefix { get; set; } = "stress";

    /// <summary>회원가입에 쓸 고정 이메일 (전체 주소). 계정마다 바꾸지 않음.</summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = "TestPass123!";

    [JsonPropertyName("saveToAccountsFile")]
    public bool SaveToAccountsFile { get; set; } = true;

    [JsonPropertyName("appendToAccountsFile")]
    public bool AppendToAccountsFile { get; set; } = false;

    public void ValidateEmail()
    {
        Email = Email.Trim();
        if (!System.Net.Mail.MailAddress.TryCreate(Email, out _))
        {
            throw new InvalidDataException(
                $"register.email must be a valid full address (e.g. you@naver.com). Current: '{Email}'");
        }
    }
}

public sealed class MatchSettings
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "pve";

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 120;

    [JsonPropertyName("parallelism")]
    public int Parallelism { get; set; } = 1;
}

public sealed class FieldSettings
{
    /// <summary>idle: HB+이동 | combat_basic: 4방향 회전+사격 루프</summary>
    [JsonPropertyName("gameplay")]
    public string Gameplay { get; set; } = "idle";

    [JsonPropertyName("sessionSeconds")]
    public int SessionSeconds { get; set; } = 60;

    [JsonPropertyName("heartbeatIntervalMs")]
    public int HeartbeatIntervalMs { get; set; } = 5000;

    [JsonPropertyName("combatStepIntervalMs")]
    public int CombatStepIntervalMs { get; set; } = 1000;

    [JsonPropertyName("moveEnabled")]
    public bool MoveEnabled { get; set; } = true;

    [JsonPropertyName("moveIntervalMs")]
    public int MoveIntervalMs { get; set; } = 200;
}

public sealed class AccountsDocument
{
    [JsonPropertyName("accounts")]
    public List<AccountEntry> Accounts { get; set; } = new();
}

public sealed class AccountEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

public sealed class LoggedInAccount
{
    public required AccountEntry Source { get; init; }
    public required long Uid { get; init; }
    public required string AuthToken { get; init; }
    public required string DeviceId { get; init; }
    public required string NickName { get; init; }

    public int WeaponSubcategory { get; set; } = 1;

    public string Label => string.IsNullOrWhiteSpace(Source.Label) ? Source.Id : Source.Label!;
}

public sealed class MatchSuccessInfo
{
    public required string Ip { get; init; }
    public required int Port { get; init; }
    public required int RoomId { get; init; }
    public int MapId { get; init; }
    public int GameMode { get; init; }
}

public enum BotRunResult
{
    Success,
    LoginFailed,
    MatchFailed,
    FieldConnectFailed,
    FieldAuthFailed,
    FieldSessionFailed,
    Error
}

public sealed class BotRunReport
{
    public required string Label { get; init; }
    public BotRunResult Result { get; init; }
    public string? Detail { get; init; }
    public MatchSuccessInfo? Match { get; init; }
    public int SentPackets { get; init; }
    public int ReceivedPackets { get; init; }
}
