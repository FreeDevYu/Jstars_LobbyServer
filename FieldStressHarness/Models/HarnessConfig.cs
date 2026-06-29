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

    [JsonPropertyName("timeouts")]
    public TimeoutSettings Timeouts { get; set; } = new();

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

        // legacy: match.timeoutSeconds / field.* → timeouts
        if (Match.TimeoutSeconds > 0)
        {
            Timeouts.MatchSuccessSeconds = Match.TimeoutSeconds;
        }

        if (Field.AuthTimeoutSeconds > 0)
        {
            Timeouts.FieldAuthSeconds = Field.AuthTimeoutSeconds;
        }

        if (Field.SessionSeconds > 0)
        {
            Timeouts.FieldSessionSeconds = Field.SessionSeconds;
        }

        Timeouts.ClampToMinimums();

        Register.ValidateEmail();
    }

    public static string NormalizeMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ArgumentException("mode is required (register | login | match | full).");
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "register" => "register",
            "login" or "login-only" => "login",
            "match" or "match-only" => "match",
            "full" => "full",
            _ => throw new ArgumentException($"Unknown mode: {raw}. Use register, login, match, or full.")
        };
    }

    public bool IsRegisterMode(string mode) =>
        string.Equals(mode, "register", StringComparison.OrdinalIgnoreCase);

    public bool IsLoginMode(string mode) =>
        string.Equals(mode, "login", StringComparison.OrdinalIgnoreCase);

    public bool IsMatchMode(string mode) =>
        string.Equals(mode, "match", StringComparison.OrdinalIgnoreCase);

    public bool IsPveMatch =>
        string.Equals(Match.Type, "pve", StringComparison.OrdinalIgnoreCase);

    public bool IsCombatBasicMode =>
        string.Equals(Field.Gameplay, "combat_basic", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public int LoginParallelism => Match.Parallelism;

    [JsonIgnore]
    public int LoginMatchBatchSize => Math.Max(1, Match.LoginMatchBatchSize);

    /// <summary>parallelism 과 accounts.json 개수 중 작은 값 = 실제 테스트 봇 수</summary>
    public int ResolveTestAccountCount(int accountsInFile) =>
        Math.Min(Math.Max(1, LoginParallelism), accountsInFile);

    [JsonIgnore]
    public int LobbyHttpTimeoutSeconds => Timeouts.LobbyHttpSeconds;

    [JsonIgnore]
    public int MatchTimeoutSeconds => Timeouts.MatchSuccessSeconds;

    [JsonIgnore]
    public int FieldAuthTimeoutSeconds => Timeouts.FieldAuthSeconds;

    [JsonIgnore]
    public int GameStartTimeoutSeconds => Timeouts.GameStartSeconds;

    [JsonIgnore]
    public int FieldSessionSeconds => Timeouts.FieldSessionSeconds;

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

    /// <summary>deprecated — use timeouts.matchSuccessSeconds</summary>
    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 0;

    [JsonPropertyName("parallelism")]
    public int Parallelism { get; set; } = 1;

    /// <summary>full 모드 — 로그인+매칭 enqueue 동시 상한 (필드/전투는 제한 없음).</summary>
    [JsonPropertyName("loginMatchBatchSize")]
    public int LoginMatchBatchSize { get; set; } = 20;
}

/// <summary>Harness 대기/타임아웃 (초). harness.json 의 timeouts 섹션.</summary>
public sealed class TimeoutSettings
{
    public const int DefaultLobbyHttpSeconds = 30;
    public const int DefaultMatchSuccessSeconds = 180;
    public const int DefaultFieldAuthSeconds = 60;
    public const int DefaultGameStartSeconds = 30;
    public const int DefaultFieldSessionSeconds = 60;

    /// <summary>로비 REST API (로그인, 캐릭터/인벤, 매칭 enqueue) HTTP 타임아웃</summary>
    [JsonPropertyName("lobbyHttpSeconds")]
    public int LobbyHttpSeconds { get; set; } = DefaultLobbyHttpSeconds;

    /// <summary>매칭 enqueue 후 SignalR MatchSuccess 최대 대기</summary>
    [JsonPropertyName("matchSuccessSeconds")]
    public int MatchSuccessSeconds { get; set; } = DefaultMatchSuccessSeconds;

    /// <summary>필드 TCP 연결 후 RESPONSE_AUTH 최대 대기</summary>
    [JsonPropertyName("fieldAuthSeconds")]
    public int FieldAuthSeconds { get; set; } = DefaultFieldAuthSeconds;

    /// <summary>EnterGameRoom(isReady) 후 NOTICE_GAME_START 최대 대기</summary>
    [JsonPropertyName("gameStartSeconds")]
    public int GameStartSeconds { get; set; } = DefaultGameStartSeconds;

    /// <summary>필드 인게임 유지 시간 (idle / combat_basic)</summary>
    [JsonPropertyName("fieldSessionSeconds")]
    public int FieldSessionSeconds { get; set; } = DefaultFieldSessionSeconds;

    public void ClampToMinimums()
    {
        LobbyHttpSeconds = Math.Max(5, LobbyHttpSeconds);
        MatchSuccessSeconds = Math.Max(10, MatchSuccessSeconds);
        FieldAuthSeconds = Math.Max(5, FieldAuthSeconds);
        GameStartSeconds = Math.Max(5, GameStartSeconds);
        FieldSessionSeconds = Math.Max(10, FieldSessionSeconds);
    }
}

public sealed class FieldSettings
{
    /// <summary>idle: HB+이동 | combat_basic: 4방향 회전+사격 루프</summary>
    [JsonPropertyName("gameplay")]
    public string Gameplay { get; set; } = "idle";

    /// <summary>deprecated — use timeouts.fieldSessionSeconds</summary>
    [JsonPropertyName("sessionSeconds")]
    public int SessionSeconds { get; set; } = 0;

    /// <summary>deprecated — use timeouts.fieldAuthSeconds</summary>
    [JsonPropertyName("authTimeoutSeconds")]
    public int AuthTimeoutSeconds { get; set; } = 0;

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

public enum HarnessRunPhase
{
    /// <summary>로그인 → 매칭 → 필드 AUTH 까지</summary>
    MatchOnly,

    /// <summary>로그인 → 매칭 → 필드 AUTH → 인게임 세션</summary>
    Full
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

    /// <summary>로그인 성공 전 실패(재시도) 횟수. 0이면 첫 시도 성공.</summary>
    public int LoginFailureCount { get; init; }
}
