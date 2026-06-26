namespace FieldStressHarness.Services;

internal static class HarnessLog
{
    private static readonly object ConsoleLock = new();

    public static void Info(string label, string message) => Write(label, message);

    public static void Error(string label, string message) => Write(label, message, isError: true);

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
