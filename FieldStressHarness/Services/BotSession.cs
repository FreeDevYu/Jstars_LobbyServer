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
        CancellationToken cancellationToken = default)
    {
        string label = BotLabel.Resolve(account, botIndex);

        try
        {
            HarnessLog.Info(label, "Starting full flow...");

            var (loginResponse, loginMs) = await lobbyApi.LoginAsync(account, deviceId, cancellationToken);
            if (loginResponse.State != LoginResultState.Success
                || loginResponse.User == null
                || string.IsNullOrWhiteSpace(loginResponse.Token))
            {
                return Fail(label, BotRunResult.LoginFailed, $"state={loginResponse.State}");
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

            HarnessLog.Info(label, $"Login OK ({loginMs}ms)");

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
                return Fail(label, BotRunResult.MatchFailed, "enqueue rejected");
            }

            HarnessLog.Info(label, $"{(config.IsPveMatch ? "PvE" : "PvP")} enqueue OK, waiting for match (max {config.MatchTimeoutSeconds}s)...");
            var match = await matchWaitTask;
            HarnessLog.Info(label, $"MatchSuccess {match.Ip}:{match.Port} room={match.RoomId}");

            await using var field = new FieldTcpSession();
            await field.ConnectAsync(match.Ip, match.Port, cancellationToken);
            HarnessLog.Info(label, "Field TCP connected");

            field.Send(FieldPacketBuilder.RequestAuth(loggedIn.Uid, match.RoomId, loggedIn.AuthToken));
            var authMessage = await field.WaitForContentAsync(Content.RESPONSE_AUTH, TimeSpan.FromSeconds(15), cancellationToken);
            var authResponse = RESPONSE_AUTH.GetRootAsRESPONSE_AUTH(new ByteBuffer(authMessage.Body));
            if (!authResponse.Success)
            {
                return Fail(label, BotRunResult.FieldAuthFailed, $"error={authResponse.ErrorCode}", match, field);
            }

            HarnessLog.Info(label, "Field AUTH OK");

            field.Send(FieldPacketBuilder.RequestEnterGameRoom(loggedIn.Uid, match.RoomId, isReady: true));

            var (px, py, pz) = await WaitForGameStartAndSpawnAsync(field, loggedIn.Uid, label, cancellationToken);

            if (config.IsCombatBasicMode)
            {
                await CombatBasicLoop.RunAsync(
                    label, field, loggedIn.Uid, loggedIn.WeaponSubcategory,
                    px, py, pz, config, cancellationToken);
                return Success(label, match, field);
            }

            await RunIdleFieldSessionAsync(label, field, config, cancellationToken);
            return Success(label, match, field);
        }
        catch (Exception ex)
        {
            HarnessLog.Error(label, $"FAILED: {ex.Message}");
            return Fail(label, BotRunResult.Error, ex.Message);
        }
    }

    private static async Task<(float X, float Y, float Z)> WaitForGameStartAndSpawnAsync(
        FieldTcpSession field,
        long uid,
        string label,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        bool hasSpawn = false;
        float px = SpawnPositions.Resolve(1).X;
        float py = SpawnPositions.Resolve(1).Y;
        float pz = SpawnPositions.Resolve(1).Z;

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
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

    private static async Task RunIdleFieldSessionAsync(
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
            if (field.TryTake(Content.NOTICE_GAME_END, out _))
            {
                HarnessLog.Info(label, "NOTICE_GAME_END");
                return;
            }

            if (DateTime.UtcNow >= nextHeartbeatAt)
            {
                field.Send(FieldPacketBuilder.RequestHeartbeat(Environment.TickCount64));
                nextHeartbeatAt = DateTime.UtcNow.AddMilliseconds(config.HeartbeatIntervalMs);
            }

            if (config.SendMovePackets && DateTime.UtcNow >= nextMoveAt)
            {
                moveX += 0.2f;
                field.Send(FieldPacketBuilder.RequestPlayerMove(moveX, 0f, 0f, 0f, 90f, 0f, 1f, 0f, 0f));
                nextMoveAt = DateTime.UtcNow.AddMilliseconds(config.MovePacketIntervalMs);
            }

            await Task.Delay(50, cancellationToken);
        }

        HarnessLog.Info(label, $"Field session finished ({config.FieldSessionSeconds}s)");
    }

    private static async Task CancelBothQueuesAsync(
        LobbyApiClient lobbyApi,
        LoggedInAccount account,
        CancellationToken cancellationToken)
    {
        await lobbyApi.CancelMatchingAsync(account, isPve: false, cancellationToken);
        await lobbyApi.CancelMatchingAsync(account, isPve: true, cancellationToken);
    }

    private static BotRunReport Success(string label, MatchSuccessInfo match, FieldTcpSession field) =>
        new()
        {
            Label = label,
            Result = BotRunResult.Success,
            Match = match,
            SentPackets = field.SentPackets,
            ReceivedPackets = field.ReceivedPackets
        };

    private static BotRunReport Fail(
        string label,
        BotRunResult result,
        string detail,
        MatchSuccessInfo? match = null,
        FieldTcpSession? field = null) =>
        new()
        {
            Label = label,
            Result = result,
            Detail = detail,
            Match = match,
            SentPackets = field?.SentPackets ?? 0,
            ReceivedPackets = field?.ReceivedPackets ?? 0
        };
}
