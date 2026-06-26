using System.Text.Json;
using FieldStressHarness.Models;

namespace FieldStressHarness.Services;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static HarnessConfig LoadHarnessConfig(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Harness config not found: {path}");
        }

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<HarnessConfig>(json, JsonOptions);
        if (config == null)
        {
            throw new InvalidDataException($"Failed to parse harness config: {path}");
        }

        if (string.IsNullOrWhiteSpace(config.LobbyBaseUrl))
        {
            throw new InvalidDataException("lobbyBaseUrl is required.");
        }

        config.LobbyBaseUrl = config.LobbyBaseUrl.TrimEnd('/');
        config.Normalize();
        return config;
    }

    public static AccountsDocument LoadAccounts(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Accounts file not found: {path}. Copy accounts.example.json to accounts.json and edit it.");
        }

        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<AccountsDocument>(json, JsonOptions);
        if (document == null || document.Accounts.Count == 0)
        {
            throw new InvalidDataException($"No accounts found in: {path}");
        }

        for (int i = 0; i < document.Accounts.Count; i++)
        {
            var account = document.Accounts[i];
            if (string.IsNullOrWhiteSpace(account.Id))
            {
                throw new InvalidDataException($"accounts[{i}].id is required.");
            }

            if (string.IsNullOrWhiteSpace(account.Password))
            {
                throw new InvalidDataException($"accounts[{i}].password is required.");
            }
        }

        return document;
    }

    public static async Task SaveAccountsAsync(string path, IReadOnlyList<AccountEntry> accounts, bool append)
    {
        AccountsDocument document;
        if (append && File.Exists(path))
        {
            var json = await File.ReadAllTextAsync(path);
            document = JsonSerializer.Deserialize<AccountsDocument>(json, JsonOptions) ?? new AccountsDocument();
        }
        else
        {
            document = new AccountsDocument();
        }

        document.Accounts.AddRange(accounts);

        var output = JsonSerializer.Serialize(document, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, output);
    }
}
