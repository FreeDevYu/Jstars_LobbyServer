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
            Console.WriteLine($"[Harness] accounts={accountsPath} ({accountsDocument.Accounts.Count})");

            if (harnessConfig.IsLoginMode(mode))
            {
                return await RunLoginOnlyAsync(harnessConfig, accountsDocument.Accounts);
            }

            Console.WriteLine($"[Harness] match={harnessConfig.Match.Type} | field={harnessConfig.Field.Gameplay} | parallelism={harnessConfig.Match.Parallelism}");
            if (harnessConfig.IsPveMatch && accountsDocument.Accounts.Count > 1)
            {
                Console.WriteLine("[Harness] Note: PvE = one solo room per account.");
            }
            Console.WriteLine();

            return await RunFullFlowAsync(harnessConfig, accountsDocument.Accounts);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Harness] ERROR: {ex.Message}");
            return 2;
        }
    }

    private static async Task<int> RunLoginOnlyAsync(HarnessConfig config, IReadOnlyList<AccountEntry> accounts)
    {
        using var lobbyApi = new LobbyApiClient(config.LobbyBaseUrl);
        int successCount = 0;

        foreach (var (account, index) in accounts.Select((entry, index) => (entry, index)))
        {
            var label = string.IsNullOrWhiteSpace(account.Label) ? account.Id : account.Label;
            var deviceId = string.IsNullOrWhiteSpace(account.DeviceId)
                ? $"stress-bot-{index + 1:D2}"
                : account.DeviceId!;

            try
            {
                var (response, elapsedMs) = await lobbyApi.LoginAsync(account, deviceId);
                if (response.State != LoginResultState.Success || response.User == null || string.IsNullOrWhiteSpace(response.Token))
                {
                    Console.WriteLine($"[FAIL] {label} | state={response.State} | {elapsedMs}ms");
                    continue;
                }

                successCount++;
                Console.WriteLine($"[OK]   {label} | uid={response.User.UID} | nick={response.User.NickName} | token={MaskToken(response.Token)} | {elapsedMs}ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FAIL] {label} | {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"[Harness] Login complete: {successCount}/{accounts.Count} succeeded.");
        return successCount == accounts.Count ? 0 : 1;
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

    private static async Task<int> RunFullFlowAsync(HarnessConfig config, IReadOnlyList<AccountEntry> accounts)
    {
        if (!config.IsPveMatch && accounts.Count < 2)
        {
            Console.Error.WriteLine("[Harness] PvP matchMode requires at least 2 accounts in accounts.json.");
            return 2;
        }

        using var lobbyApi = new LobbyApiClient(config.LobbyBaseUrl);
        using var semaphore = new SemaphoreSlim(Math.Max(1, config.LoginParallelism));
        var botSession = new BotSession();
        var reports = new BotRunReport?[accounts.Count];

        var tasks = accounts.Select(async (account, index) =>
        {
            await semaphore.WaitAsync();
            try
            {
                var deviceId = string.IsNullOrWhiteSpace(account.DeviceId)
                    ? $"stress-bot-{index + 1:D2}"
                    : account.DeviceId!;

                reports[index] = await botSession.RunAsync(account, index, deviceId, config, lobbyApi);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        var finalReports = reports.Where(report => report != null).Select(report => report!).ToList();
        int successCount = finalReports.Count(report => report.Result == BotRunResult.Success);

        Console.WriteLine();
        foreach (var report in finalReports)
        {
            string matchInfo = report.Match == null
                ? "-"
                : $"{report.Match.Ip}:{report.Match.Port} room={report.Match.RoomId}";

            if (report.Result == BotRunResult.Success)
            {
                Console.WriteLine(
                    $"[DONE] {report.Label} | OK | match={matchInfo} | sent={report.SentPackets} recv={report.ReceivedPackets}");
            }
            else
            {
                Console.WriteLine(
                    $"[DONE] {report.Label} | {report.Result} | {report.Detail} | match={matchInfo}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"[Harness] Full flow complete: {successCount}/{accounts.Count} succeeded.");
        return successCount == accounts.Count ? 0 : 1;
    }

    private static string ResolveRunMode(CliOptions options)
    {
        string? raw = options.Mode;
        if (string.IsNullOrWhiteSpace(raw))
        {
            Console.Write("mode (register / login / full): ");
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
                  dotnet run --project FieldStressHarness -- --mode <register|login|full> [options]

                mode (required — pass on CLI or type when prompted):
                  register   create accounts -> accounts.json
                  login      login test only
                  full       login -> match -> field

                Settings: harness.json (one file, all sections)

                Options:
                  --mode <name>       register | login | full
                  --config <path>     default: harness.json
                  --accounts <path>   accounts.json
                  --lobby <url>
                  -h, --help
                """);
        }
    }
}
