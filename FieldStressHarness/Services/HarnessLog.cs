namespace FieldStressHarness.Services;

internal static class HarnessLog
{
    private static readonly object ConsoleLock = new();

    public static void Info(string label, string message) => Write(label, message);

    public static void Error(string label, string message) => Write(label, message, isError: true);

    /// <summary>배치 종료 요약 — stderr + 구분선으로 combat 등 대량 로그 뒤에서도 잘 보이게.</summary>
    public static void PrintBatchSummary(
        string title,
        int successCount,
        int totalCount,
        string elapsed,
        IReadOnlyDictionary<int, int>? loginFailureHistogram = null)
    {
        const string bar = "================================================================================";
        lock (ConsoleLock)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(bar);
            Console.Error.WriteLine($"  {title}");
            Console.Error.WriteLine($"  Result : {successCount}/{totalCount} succeeded");
            if (loginFailureHistogram is { Count: > 0 })
            {
                foreach (var (failureCount, accountCount) in loginFailureHistogram.OrderBy(entry => entry.Key))
                {
                    Console.Error.WriteLine(
                        $"           login failed {failureCount} time(s): {accountCount} account(s)");
                }
            }

            Console.Error.WriteLine($"  Elapsed: {elapsed}");
            Console.Error.WriteLine(bar);
            Console.Error.WriteLine();
        }
    }

    private static void Write(string label, string message, bool isError = false)
    {
        lock (ConsoleLock)
        {
            if (isError)
            {
                Console.Error.WriteLine($"[{label}] {message}");
            }
            else
            {
                Console.WriteLine($"[{label}] {message}");
            }
        }
    }
}
