using FieldStressHarness.Models;
using FieldStressHarness.Protocol;

namespace FieldStressHarness.Services;

internal static class LoginRetry
{
    public const int DelayMs = 1000;

    /// <summary>첫 시도 실패 후 추가 재시도 횟수.</summary>
    public const int MaxRetries = 2;

    public static int MaxAttempts => 1 + MaxRetries;

    public static async Task<(LoginResponse Response, long ElapsedMs, int FailedAttempts)> UntilSuccessAsync(
        LobbyApiClient lobbyApi,
        AccountEntry account,
        string deviceId,
        string label,
        CancellationToken cancellationToken = default)
    {
        int failedAttempts = 0;
        LoginResultState lastRetryableState = LoginResultState.Unknown;
        long lastElapsedMs = 0;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var (response, elapsedMs) = await lobbyApi.LoginAsync(account, deviceId, cancellationToken);
                if (IsSuccess(response))
                {
                    return (response, elapsedMs, failedAttempts);
                }

                if (!IsRetryable(response.State))
                {
                    throw new LoginPermanentFailureException(
                        response.State, failedAttempts + 1, elapsedMs);
                }

                failedAttempts++;
                lastRetryableState = response.State;
                lastElapsedMs = elapsedMs;

                if (attempt >= MaxAttempts)
                {
                    throw new LoginPermanentFailureException(
                        lastRetryableState, failedAttempts, lastElapsedMs);
                }

                HarnessLog.Info(
                    label,
                    $"Login retry | state={response.State} | failed={failedAttempts}/{MaxRetries} | attempt={attempt}/{MaxAttempts} | {elapsedMs}ms");
            }
            catch (LoginPermanentFailureException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failedAttempts++;
                lastRetryableState = LoginResultState.Unknown;
                lastElapsedMs = 0;

                if (attempt >= MaxAttempts)
                {
                    throw new LoginPermanentFailureException(
                        lastRetryableState, failedAttempts, lastElapsedMs);
                }

                HarnessLog.Info(
                    label,
                    $"Login retry | failed={failedAttempts}/{MaxRetries} | attempt={attempt}/{MaxAttempts} | {ex.Message}");
            }

            if (attempt < MaxAttempts)
            {
                await Task.Delay(DelayMs, cancellationToken);
            }
        }

        throw new LoginPermanentFailureException(lastRetryableState, failedAttempts, lastElapsedMs);
    }

    public static bool IsSuccess(LoginResponse response) =>
        response.State == LoginResultState.Success
        && response.User != null
        && !string.IsNullOrWhiteSpace(response.Token);

    public static bool IsRetryable(LoginResultState state) =>
        state is LoginResultState.Unknown or LoginResultState.InvalidToken;
}

internal sealed class LoginPermanentFailureException : Exception
{
    public LoginResultState State { get; }
    public int FailedAttempts { get; }
    public long ElapsedMs { get; }

    public LoginPermanentFailureException(LoginResultState state, int failedAttempts, long elapsedMs)
        : base($"state={state}")
    {
        State = state;
        FailedAttempts = failedAttempts;
        ElapsedMs = elapsedMs;
    }
}
