using FieldStressHarness.FieldNet;
using FieldStressHarness.Models;
using FieldStressHarness.Protocol;
using Google.FlatBuffers;
using protocol;

namespace FieldStressHarness.Services;

public sealed class BotSession
{
    public async Task<BotRunReport> RunAsync(
        AccountEntry account,
        int botIndex,
        string deviceId,
        HarnessConfig config,
        LobbyApiClient lobbyApi,
        HarnessRunPhase phase = HarnessRunPhase.Full,
        SemaphoreSlim? loginMatchGate = null,
        CancellationToken cancellationToken = default)
    {
        string label = BotLabel.Resolve(account, botIndex);
        int loginFailureCount = 0;
        MatchSuccessInfo? match = null;
        FieldTcpSession? field = null;

        try
        {
            string flowName = phase == HarnessRunPhase.MatchOnly ? "match flow" : "full flow";
            HarnessLog.Info(label, $"Starting {flowName}...");

            if (loginMatchGate != null)
            {
                await loginMatchGate.WaitAsync(cancellationToken);
            }

            LobbyMatchResult lobbyResult;
            try
            {
                lobbyResult = await RunLobbyLoginAndMatchAsync(
                    account, botIndex, deviceId, config, lobbyApi, label, cancellationToken);
            }
            finally
            {
                loginMatchGate?.Release();
            }

            label = lobbyResult.Label;
            loginFailureCount = lobbyResult.LoginFailureCount;
            match = lobbyResult.Match;

            field = new FieldTcpSession();
            await field.ConnectAsync(match.Ip, match.Port, cancellationToken);
            HarnessLog.Info(label, "Field TCP connected");

            field.Send(FieldPacketBuilder.RequestAuth(
                lobbyResult.LoggedIn.Uid, match.RoomId, lobbyResult.LoggedIn.AuthToken));
            var authMessage = await field.WaitForContentAsync(
                Content.RESPONSE_AUTH,
                TimeSpan.FromSeconds(config.FieldAuthTimeoutSeconds),
                cancellationToken);
            var authResponse = RESPONSE_AUTH.GetRootAsRESPONSE_AUTH(new ByteBuffer(authMessage.Body));
            if (!authResponse.Success)
            {
                return Fail(
                    label,
                    BotRunResult.FieldAuthFailed,
                    $"error={authResponse.ErrorCode}",
                    match,
                    field,
                    loginFailureCount);
            }

            HarnessLog.Info(label, "Field AUTH OK");

            if (phase == HarnessRunPhase.MatchOnly)
            {
                return Success(label, match, field, loginFailureCount);
            }

            field.Send(FieldPacketBuilder.RequestEnterGameRoom(
                lobbyResult.LoggedIn.Uid, match.RoomId, isReady: true));

            var (px, py, pz) = await WaitForGameStartAndSpawnAsync(
                field, lobbyResult.LoggedIn.Uid, label, config, cancellationToken);
            if (FieldHeartbeat.IsDisconnected(field))
            {
                return Fail(
                    label,
                    BotRunResult.FieldSessionFailed,
                    "Field TCP disconnected while waiting for game start (heartbeat timeout?)",
                    match,
                    field,
                    loginFailureCount);
            }

            if (config.IsCombatBasicMode)
            {
                bool completed = await CombatBasicLoop.RunAsync(
                    label, field, lobbyResult.LoggedIn.Uid, lobbyResult.LoggedIn.WeaponSubcategory,
                    px, py, pz, config, cancellationToken);
                if (!completed)
                {
                    return Fail(
                        label,
                        BotRunResult.FieldSessionFailed,
                        "Field TCP disconnected during combat session",
                        match,
                        field,
                        loginFailureCount);
                }

                return Success(label, match, field, loginFailureCount);
            }

            bool idleCompleted = await RunIdleFieldSessionAsync(label, field, config, cancellationToken);
            if (!idleCompleted)
            {
                return Fail(
                    label,
                    BotRunResult.FieldSessionFailed,
                    "Field TCP disconnected during field session",
                    match,
                    field,
                    loginFailureCount);
            }

            return Success(label, match, field, loginFailureCount);
        }
        catch (BotFlowException ex)
        {
            return ex.Report;
        }
        catch (Exception ex)
        {
            HarnessLog.Error(label, $"FAILED: {ex.Message}");
            return Fail(label, BotRunResult.Error, ex.Message, match, field, loginFailureCount);
        }
        finally
        {
            if (field != null)
            {
                await field.DisposeAsync();
            }
        }
    }

    private sealed record LobbyMatchResult(
        LoggedInAccount LoggedIn,
        MatchSuccessInfo Match,
        string Label,
        int LoginFailureCount);

    private static async Task<LobbyMatchResult> RunLobbyLoginAndMatchAsync(
        AccountEntry account,
        int botIndex,
        string deviceId,
        HarnessConfig config,
        LobbyApiClient lobbyApi,
        string label,
        CancellationToken cancellationToken)
    {
        int loginFailureCount;
        LoginResponse loginResponse;
        long loginMs;
        try
        {
            (loginResponse, loginMs, loginFailureCount) = await LoginRetry.UntilSuccessAsync(
                lobbyApi, account, deviceId, label, cancellationToken);
        }
        catch (LoginPermanentFailureException ex)
        {
            throw new BotFlowException(Fail(
                label,
                BotRunResult.LoginFailed,
                $"state={ex.State} | login failed {ex.FailedAttempts} time(s)",
                loginFailureCount: ex.FailedAttempts));
        }

        if (loginResponse.User == null || string.IsNullOrWhiteSpace(loginResponse.Token))
        {
            throw new BotFlowException(Fail(label, BotRunResult.LoginFailed, "empty user or token"));
        }

        label = BotLabel.Resolve(account, botIndex, loginResponse.User.UID);

        var loggedIn = new LoggedInAccount
        {
            Source = account,
            Uid = loginResponse.User.UID,
            AuthToken = loginResponse.Token,
            DeviceId = deviceId,
            NickName = loginResponse.User.NickName
        };

        HarnessLog.Info(label, loginFailureCount > 0
            ? $"Login OK ({loginMs}ms, after {loginFailureCount} failed attempt(s))"
            : $"Login OK ({loginMs}ms)");

        loggedIn.WeaponSubcategory = await lobbyApi.PrepareLobbyDataAsync(loggedIn, cancellationToken);
        HarnessLog.Info(label, $"Lobby data loaded (weaponSubcategory={loggedIn.WeaponSubcategory})");

        await using var signalR = new LobbySignalRClient();
        await signalR.ConnectAsync(config.LobbyBaseUrl, loggedIn, cancellationToken);
        HarnessLog.Info(label, "SignalR connected");

        await CancelBothQueuesAsync(lobbyApi, loggedIn, cancellationToken);

        var matchWaitTask = signalR.WaitForMatchSuccessAsync(
            TimeSpan.FromSeconds(config.MatchTimeoutSeconds),
            cancellationToken);

        var (enqueueResponse, _) = await lobbyApi.EnqueueMatchingAsync(loggedIn, config.IsPveMatch, cancellationToken);
        if (!enqueueResponse.Success)
        {
            throw new BotFlowException(Fail(
                label,
                BotRunResult.MatchFailed,
                "enqueue rejected",
                loginFailureCount: loginFailureCount));
        }

        HarnessLog.Info(
            label,
            $"{(config.IsPveMatch ? "PvE" : "PvP")} enqueue OK, waiting for match (max {config.MatchTimeoutSeconds}s)...");
        var match = await matchWaitTask;
        HarnessLog.Info(label, $"MatchSuccess {match.Ip}:{match.Port} room={match.RoomId}");

        return new LobbyMatchResult(loggedIn, match, label, loginFailureCount);
    }

    private static async Task<(float X, float Y, float Z)> WaitForGameStartAndSpawnAsync(
        FieldTcpSession field,
        long uid,
        string label,
        HarnessConfig config,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(config.GameStartTimeoutSeconds);
        var nextHeartbeatAt = DateTime.UtcNow;
        bool hasSpawn = false;
        float px = SpawnPositions.Resolve(1).X;
        float py = SpawnPositions.Resolve(1).Y;
        float pz = SpawnPositions.Resolve(1).Z;

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (FieldHeartbeat.IsDisconnected(field))
            {
                throw new IOException("Field TCP disconnected while waiting for NOTICE_GAME_START.");
            }

            FieldHeartbeat.TrySend(field, config.HeartbeatIntervalMs, ref nextHeartbeatAt);

            if (field.TryTake(Content.NOTICE_GAME_START, out _))
            {
                HarnessLog.Info(label, $"NOTICE_GAME_START spawn=({px:F1},{py:F1},{pz:F1})");
                return (px, py, pz);
            }

            if (!hasSpawn && field.TryTake(Content.NOTICE_PLAYER_SPAWN, out var message) && message != null)
            {
                var spawn = NOTICE_PLAYER_SPAWN.GetRootAsNOTICE_PLAYER_SPAWN(new ByteBuffer(message.Body));
                if (spawn.UserKey == uid)
                {
                    (px, py, pz) = SpawnPositions.Resolve(spawn.SpawnPositionId);
                    hasSpawn = true;
                }
            }

            await Task.Delay(50, cancellationToken);
        }

        throw new TimeoutException("Timed out waiting for NOTICE_GAME_START.");
    }

    private static async Task<bool> RunIdleFieldSessionAsync(
        string label,
        FieldTcpSession field,
        HarnessConfig config,
        CancellationToken cancellationToken)
    {
        var sessionEndAt = DateTime.UtcNow.AddSeconds(config.FieldSessionSeconds);
        var nextHeartbeatAt = DateTime.UtcNow;
        var nextMoveAt = DateTime.UtcNow;
        float moveX = 0f;

        while (DateTime.UtcNow < sessionEndAt && !cancellationToken.IsCancellationRequested)
        {
            if (FieldHeartbeat.IsDisconnected(field))
            {
                HarnessLog.Info(label, "Field TCP disconnected");
                return false;
            }

            if (field.TryTake(Content.NOTICE_GAME_END, out _))
            {
                HarnessLog.Info(label, "NOTICE_GAME_END");
                return true;
            }

            FieldHeartbeat.TrySend(field, config.HeartbeatIntervalMs, ref nextHeartbeatAt);

            if (config.SendMovePackets && DateTime.UtcNow >= nextMoveAt)
            {
                moveX += 0.2f;
                field.Send(FieldPacketBuilder.RequestPlayerMove(moveX, 0f, 0f, 0f, 90f, 0f, 1f, 0f, 0f));
                nextMoveAt = DateTime.UtcNow.AddMilliseconds(config.MovePacketIntervalMs);
            }

            await Task.Delay(50, cancellationToken);
        }

        HarnessLog.Info(label, $"Field session finished ({config.FieldSessionSeconds}s)");
        return true;
    }

    private static async Task CancelBothQueuesAsync(
        LobbyApiClient lobbyApi,
        LoggedInAccount account,
        CancellationToken cancellationToken)
    {
        await lobbyApi.CancelMatchingAsync(account, isPve: false, cancellationToken);
        await lobbyApi.CancelMatchingAsync(account, isPve: true, cancellationToken);
    }

    private static BotRunReport Success(
        string label,
        MatchSuccessInfo match,
        FieldTcpSession field,
        int loginFailureCount = 0) =>
        new()
        {
            Label = label,
            Result = BotRunResult.Success,
            Match = match,
            SentPackets = field.SentPackets,
            ReceivedPackets = field.ReceivedPackets,
            LoginFailureCount = loginFailureCount
        };

    private static BotRunReport Fail(
        string label,
        BotRunResult result,
        string detail,
        MatchSuccessInfo? match = null,
        FieldTcpSession? field = null,
        int loginFailureCount = 0) =>
        new()
        {
            Label = label,
            Result = result,
            Detail = detail,
            Match = match,
            SentPackets = field?.SentPackets ?? 0,
            ReceivedPackets = field?.ReceivedPackets ?? 0,
            LoginFailureCount = loginFailureCount
        };

    private sealed class BotFlowException : Exception
    {
        public BotRunReport Report { get; }

        public BotFlowException(BotRunReport report) : base(report.Detail)
        {
            Report = report;
        }
    }
}
