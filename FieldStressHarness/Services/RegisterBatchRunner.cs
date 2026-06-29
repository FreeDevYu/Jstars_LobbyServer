using FieldStressHarness.Models;
using FieldStressHarness.Protocol;

namespace FieldStressHarness.Services;

public sealed class RegisterBatchRunner
{
    public async Task<IReadOnlyList<AccountEntry>> RunAsync(
        HarnessConfig config,
        string accountsOutputPath,
        CancellationToken cancellationToken = default)
    {
        using var lobbyApi = new LobbyApiClient(
            config.LobbyBaseUrl,
            TimeSpan.FromSeconds(config.LobbyHttpTimeoutSeconds));
        var created = new List<AccountEntry>();
        int count = Math.Max(1, config.Register.Count);
        string password = config.Register.Password;
        string idPrefix = config.Register.IdPrefix;
        string email = config.Register.Email;

        for (int i = 0; i < count; i++)
        {
            int index = i + 1;
            string id = $"{idPrefix}{index:D2}";
            string label = $"reg-{id}";

            try
            {
                var (idCheck, _) = await lobbyApi.UsingIdCheckAsync(id, cancellationToken);
                if (!idCheck.Success)
                {
                    HarnessLog.Error(label, "ID already in use");
                    continue;
                }

                var (response, elapsedMs) = await lobbyApi.TestRegisterAsync(id, password, email, cancellationToken);
                if (!response.Success)
                {
                    string detail = string.IsNullOrWhiteSpace(response.Detail) ? response.State.ToString() : response.Detail;
                    HarnessLog.Error(label, $"register failed: {detail} ({elapsedMs}ms) email={email}");
                    continue;
                }

                var account = new AccountEntry
                {
                    Id = id,
                    Password = password,
                    Label = label,
                    DeviceId = $"stress-bot-{index:D2}"
                };

                created.Add(account);
                HarnessLog.Info(label, $"registered OK ({elapsedMs}ms) email={email}");
            }
            catch (Exception ex)
            {
                HarnessLog.Error(label, ex.Message);
            }
        }

        if (!config.Register.SaveToAccountsFile)
        {
            HarnessLog.Info("register", "saveToAccountsFile=false — skipping accounts.json write");
            return created;
        }

        if (created.Count == 0)
        {
            HarnessLog.Info("register", "no successful registrations — skipping accounts.json write");
            return created;
        }

        await ConfigLoader.SaveAccountsAsync(accountsOutputPath, created, config.Register.AppendToAccountsFile);
        HarnessLog.Info("register", $"wrote {created.Count} account(s) -> {Path.GetFullPath(accountsOutputPath)}");

        return created;
    }
}
