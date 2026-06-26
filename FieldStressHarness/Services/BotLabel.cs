using FieldStressHarness.Models;

namespace FieldStressHarness.Services;

internal static class BotLabel
{
    public static string Resolve(AccountEntry account, int index, long? uid = null)
    {
        string baseLabel = string.IsNullOrWhiteSpace(account.Label) ? account.Id : account.Label!;
        return uid.HasValue ? $"{baseLabel}(uid={uid.Value})" : $"{baseLabel}(#{index + 1})";
    }
}
