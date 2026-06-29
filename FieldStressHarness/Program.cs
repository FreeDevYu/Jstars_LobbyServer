using System.Diagnostics;
using FieldStressHarness.Models;
using FieldStressHarness.Protocol;
using FieldStressHarness.Services;

namespace FieldStressHarness;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);
            string mode = ResolveRunMode(options);

            var harnessConfig = ConfigLoader.LoadHarnessConfig(options.ConfigPath);
            string configDirectory = Path.GetDirectoryName(options.ConfigPath)
                ?? throw new InvalidOperationException($"Invalid config path: {options.ConfigPath}");

            if (!string.IsNullOrWhiteSpace(options.LobbyBaseUrl))
            {
                harnessConfig.LobbyBaseUrl = options.LobbyBaseUrl.TrimEnd('/');
            }

            var accountsPath = ResolvePath(options.AccountsPath ?? harnessConfig.AccountsFile, configDirectory);

            Console.WriteLine($"[Harness] Lobby: {harnessConfig.LobbyBaseUrl}");
            Console.WriteLine($"[Harness] mode={mode}");
            Console.WriteLine();

            if (harnessConfig.IsRegisterMode(mode))
            {
                var reg = harnessConfig.Register;
                Console.WriteLine($"[Harness] register x{reg.Count} | id={reg.IdPrefix}01.. | email={reg.Email} | out={accountsPath}");
                Console.WriteLine("[Harness] Note: TestAuth/Register does not send SMTP mail.");
                Console.WriteLine();
                return await RunRegisterAsync(harnessConfig, accountsPath);
            }

            var accountsDocument = ConfigLoader.LoadAccounts(accountsPath);
            var testAccounts = TakeTestAccounts(harnessConfig, accountsDocument.Accounts);
            Console.WriteLine(
                $"[Harness] accounts={accountsPath} ({accountsDocument.Accounts.Count} in file, running {testAccounts.Count})");

            if (harnessConfig.IsLoginMode(mode))
            {
                Console.WriteLine($"[Harness] login parallelism={harnessConfig.Match.Parallelism} | max {LoginRetry.MaxRetries} login retries per account");
                Console.WriteLine();
                return await RunLoginOnlyAsync(harnessConfig, testAccounts);
            }

            if (harnessConfig.IsMatchMode(mode))
            {
                Console.WriteLine($"[Harness] match={harnessConfig.Match.Type} | parallelism={harnessConfig.Match.Parallelism} | max {LoginRetry.MaxRetries} login retries per account");
                Console.WriteLine($"[Harness] timeouts: lobbyHttp={harnessConfig.LobbyHttpTimeoutSeconds}s | matchSuccess={harnessConfig.MatchTimeoutSeconds}s | fieldAuth={harnessConfig.FieldAuthTimeoutSeconds}s");
                if (harnessConfig.IsPveMatch && testAccounts.Count > 1)
                {
                    Console.WriteLine("[Harness] Note: PvE = one solo room per account.");
                }
                Console.WriteLine();

                return await RunBotBatchAsync(
                    harnessConfig,
                    testAccounts,
                    HarnessRunPhase.MatchOnly,
                    "MATCH COMPLETE");
            }

            Console.WriteLine($"[Harness] match={harnessConfig.Match.Type} | field={harnessConfig.Field.Gameplay} | parallelism={harnessConfig.Match.Parallelism} | login/match batch={harnessConfig.LoginMatchBatchSize}");
            Console.WriteLine($"[Harness] timeouts: lobbyHttp={harnessConfig.LobbyHttpTimeoutSeconds}s | matchSuccess={harnessConfig.MatchTimeoutSeconds}s | fieldAuth={harnessConfig.FieldAuthTimeoutSeconds}s | gameStart={harnessConfig.GameStartTimeoutSeconds}s | fieldSession={harnessConfig.FieldSessionSeconds}s");
            if (harnessConfig.IsPveMatch && testAccounts.Count > 1)
            {
                Console.WriteLine("[Harness] Note: PvE = one solo room per account.");
            }
            Console.WriteLine();

            return await RunBotBatchAsync(
                harnessConfig,
                testAccounts,
                HarnessRunPhase.Full,
                "FULL FLOW COMPLETE");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Harness] ERROR: {ex.Message}");
            return 2;
        }
    }

    private static async Task<int> RunLoginOnlyAsync(HarnessConfig config, IReadOnlyList<AccountEntry> accounts)
    {
        var stopwatch = Stopwatch.StartNew();
        using var lobbyApi = new LobbyApiClient(
            config.LobbyBaseUrl,
            TimeSpan.FromSeconds(config.LobbyHttpTimeoutSeconds));
        using var semaphore = new SemaphoreSlim(Math.Max(1, config.LoginParallelism));
        var consoleLock = new object();

        var outcomes = await Task.WhenAll(accounts.Select((account, index) =>
            LoginAccountAsync(
                account, index, lobbyApi, semaphore, consoleLock)));

        stopwatch.Stop();
        int successCount = outcomes.Count(outcome => outcome.Success);
        var loginFailureHistogram = BuildLoginFailureHistogram(
            outcomes.Select(outcome => outcome.LoginFailureCount));

        HarnessLog.PrintBatchSummary(
            "LOGIN COMPLETE",
            successCount,
            accounts.Count,
            FormatElapsed(stopwatch.Elapsed),
            loginFailureHistogram);
        return successCount == accounts.Count ? 0 : 1;
    }

    private sealed record LoginOutcome(bool Success, int LoginFailureCount);

    private static async Task<LoginOutcome> LoginAccountAsync(
        AccountEntry account,
        int index,
        LobbyApiClient lobbyApi,
        SemaphoreSlim semaphore,
        object consoleLock)
    {
        string label = string.IsNullOrWhiteSpace(account.Label) ? account.Id : account.Label;
        var deviceId = string.IsNullOrWhiteSpace(account.DeviceId)
            ? $"stress-bot-{index + 1:D2}"
            : account.DeviceId!;

        await semaphore.WaitAsync();
        try
        {
            var (response, elapsedMs, failedAttempts) = await LoginRetry.UntilSuccessAsync(
                lobbyApi, account, deviceId, label);
            lock (consoleLock)
            {
                string retryNote = failedAttempts > 0 ? $" | login failed {failedAttempts} time(s) before success" : string.Empty;
                Console.WriteLine(
                    $"[OK]   {label} | uid={response.User!.UID} | nick={response.User.NickName} | token={MaskToken(response.Token!)}{retryNote} | {elapsedMs}ms");
            }

            return new LoginOutcome(true, failedAttempts);
        }
        catch (LoginPermanentFailureException ex)
        {
            lock (consoleLock)
            {
                Console.WriteLine(
                    $"[FAIL] {label} | state={ex.State} | login failed {ex.FailedAttempts} time(s) (not retryable) | {ex.ElapsedMs}ms");
            }

            return new LoginOutcome(false, ex.FailedAttempts);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async Task<int> RunRegisterAsync(HarnessConfig config, string accountsOutputPath)
    {
        var runner = new RegisterBatchRunner();
        var created = await runner.RunAsync(config, accountsOutputPath);

        Console.WriteLine();
        Console.WriteLine($"[Harness] Register complete: {created.Count}/{config.Register.Count} succeeded.");
        if (created.Count == 0)
        {
            Console.WriteLine("[Harness] accounts.json was not written (no successful registrations).");
        }
        else if (!config.Register.SaveToAccountsFile)
        {
            Console.WriteLine("[Harness] accounts.json was not written (register.saveToAccountsFile=false).");
        }

        return created.Count == config.Register.Count ? 0 : 1;
    }

    private static async Task<int> RunBotBatchAsync(
        HarnessConfig config,
        IReadOnlyList<AccountEntry> accounts,
        HarnessRunPhase phase,
        string summaryTitle)
    {
        if (!config.IsPveMatch && accounts.Count < 2)
        {
            Console.Error.WriteLine("[Harness] PvP matchMode requires at least 2 accounts in accounts.json.");
            return 2;
        }

        using var lobbyApi = new LobbyApiClient(
            config.LobbyBaseUrl,
            TimeSpan.FromSeconds(config.LobbyHttpTimeoutSeconds));
        using var runGate = phase == HarnessRunPhase.Full
            ? null
            : new SemaphoreSlim(Math.Max(1, config.LoginParallelism));
        using var loginMatchGate = phase == HarnessRunPhase.Full
            ? new SemaphoreSlim(config.LoginMatchBatchSize)
            : null;
        var botSession = new BotSession();
        var reports = new BotRunReport?[accounts.Count];

        var stopwatch = Stopwatch.StartNew();
        var tasks = accounts.Select(async (account, index) =>
        {
            if (runGate != null)
            {
                await runGate.WaitAsync();
            }

            try
            {
                var deviceId = string.IsNullOrWhiteSpace(account.DeviceId)
                    ? $"stress-bot-{index + 1:D2}"
                    : account.DeviceId!;

                reports[index] = await botSession.RunAsync(
                    account, index, deviceId, config, lobbyApi, phase, loginMatchGate);
            }
            finally
            {
                runGate?.Release();
            }
        });

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        var finalReports = reports.Where(report => report != null).Select(report => report!).ToList();
        int successCount = finalReports.Count(report => report.Result == BotRunResult.Success);

        var failedReports = finalReports
            .Where(report => report.Result != BotRunResult.Success)
            .ToList();

        if (failedReports.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"[Harness] {failedReports.Count} bot(s) failed:");
            foreach (var report in failedReports)
            {
                string matchInfo = report.Match == null
                    ? "-"
                    : $"{report.Match.Ip}:{report.Match.Port} room={report.Match.RoomId}";

                Console.WriteLine(
                    $"[FAIL] {report.Label} | {report.Result} | {report.Detail} | match={matchInfo}");
            }
        }

        if (phase == HarnessRunPhase.MatchOnly)
        {
            var loginRetriedReports = finalReports
                .Where(report => report.Result == BotRunResult.Success && report.LoginFailureCount > 0)
                .ToList();

            if (loginRetriedReports.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"[Harness] {loginRetriedReports.Count} bot(s) recovered after login retry:");
                foreach (var report in loginRetriedReports)
                {
                    string matchInfo = report.Match == null
                        ? "-"
                        : $"{report.Match.Ip}:{report.Match.Port} room={report.Match.RoomId}";

                    Console.WriteLine(
                        $"[LOGIN-RETRY] {report.Label} | login failed {report.LoginFailureCount} time(s) before success | match={matchInfo}");
                }
            }
        }

        var loginFailureHistogram = BuildLoginFailureHistogram(
            finalReports.Select(report => report.LoginFailureCount));

        HarnessLog.PrintBatchSummary(
            summaryTitle,
            successCount,
            accounts.Count,
            FormatElapsed(stopwatch.Elapsed),
            phase == HarnessRunPhase.MatchOnly ? loginFailureHistogram : null);
        return successCount == accounts.Count ? 0 : 1;
    }

    private static Dictionary<int, int> BuildLoginFailureHistogram(IEnumerable<int> loginFailureCounts) =>
        loginFailureCounts
            .GroupBy(count => count)
            .ToDictionary(group => group.Key, group => group.Count());

    private static List<AccountEntry> TakeTestAccounts(
        HarnessConfig config,
        IReadOnlyList<AccountEntry> accounts) =>
        accounts.Take(config.ResolveTestAccountCount(accounts.Count)).ToList();

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
        {
            return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m {elapsed.Seconds}.{elapsed.Milliseconds:D3}s";
        }

        if (elapsed.TotalMinutes >= 1)
        {
            return $"{elapsed.Minutes}m {elapsed.Seconds}.{elapsed.Milliseconds:D3}s";
        }

        return $"{elapsed.TotalSeconds:F1}s";
    }

    private static string ResolveRunMode(CliOptions options)
    {
        string? raw = options.Mode;
        if (string.IsNullOrWhiteSpace(raw))
        {
            Console.Write("mode (register / login / match / full): ");
            raw = Console.ReadLine();
        }

        return HarnessConfig.NormalizeMode(raw);
    }

    private static string ResolvePath(string path, string workingDirectory)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(workingDirectory, path));
    }

    private static string MaskToken(string token)
    {
        if (token.Length <= 8)
        {
            return "****";
        }

        return $"{token[..4]}...{token[^4..]}";
    }

    private static string ResolveContentRoot(string? configPathArg)
    {
        if (!string.IsNullOrWhiteSpace(configPathArg))
        {
            string candidate = Path.IsPathRooted(configPathArg)
                ? configPathArg
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configPathArg));
            if (File.Exists(candidate))
            {
                return Path.GetDirectoryName(candidate)!;
            }
        }

        string cwd = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(cwd, "harness.json")))
        {
            return cwd;
        }

        string baseDir = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(baseDir, "harness.json")))
        {
            return baseDir;
        }

        return cwd;
    }

    private sealed class CliOptions
    {
        public string ConfigPath { get; set; } = "harness.json";
        public string? AccountsPath { get; set; }
        public string? LobbyBaseUrl { get; set; }
        public string? Mode { get; set; }

        public static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();
            string? configPathArg = null;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--config" when i + 1 < args.Length:
                        options.ConfigPath = configPathArg = args[++i];
                        break;
                    case "--accounts" when i + 1 < args.Length:
                        options.AccountsPath = args[++i];
                        break;
                    case "--lobby" when i + 1 < args.Length:
                        options.LobbyBaseUrl = args[++i];
                        break;
                    case "--mode" when i + 1 < args.Length:
                        options.Mode = args[++i];
                        break;
                    case "--help":
                    case "-h":
                        PrintHelp();
                        Environment.Exit(0);
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument: {args[i]}");
                }
            }

            string contentRoot = ResolveContentRoot(configPathArg);
            options.ConfigPath = Path.IsPathRooted(options.ConfigPath)
                ? options.ConfigPath
                : Path.GetFullPath(Path.Combine(contentRoot, options.ConfigPath));
            return options;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("""
                FieldStressHarness

                Usage:
                  dotnet run --project FieldStressHarness -- --mode <register|login|match|full> [options]

                mode (required — pass on CLI or type when prompted):
                  register   create accounts -> accounts.json
                  login      concurrent login test (retry until all succeed)
                  match      login -> match -> field AUTH
                  full       login -> match -> field (in-game session)

                Settings: harness.json (one file, all sections)

                Options:
                  --mode <name>       register | login | match | full
                  --config <path>     default: harness.json
                  --accounts <path>   accounts.json
                  --lobby <url>
                  -h, --help
                """);
        }
    }
}
