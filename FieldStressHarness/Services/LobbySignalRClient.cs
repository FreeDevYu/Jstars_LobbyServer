using System.Text.Json;
using FieldStressHarness.Models;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace FieldStressHarness.Services;

public sealed class LobbySignalRClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true
    };

    private HubConnection? _connection;
    private TaskCompletionSource<MatchSuccessInfo>? _matchSuccessTcs;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(string lobbyBaseUrl, LoggedInAccount account, CancellationToken cancellationToken = default)
    {
        await DisposeAsync();

        var hubUri = BuildHubUri(lobbyBaseUrl, account.Uid);
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(account.AuthToken);
                options.Headers.Add("ID", account.Source.Id);
                options.Headers.Add("DeviceID", account.DeviceId);
                options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<JsonElement>("MatchSuccess", OnMatchSuccessJson);
        _connection.Closed += _ => Task.CompletedTask;

        await _connection.StartAsync(cancellationToken);
    }

    public Task<MatchSuccessInfo> WaitForMatchSuccessAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        _matchSuccessTcs = new TaskCompletionSource<MatchSuccessInfo>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        timeoutCts.Token.Register(() => _matchSuccessTcs.TrySetCanceled(timeoutCts.Token));
        return _matchSuccessTcs.Task;
    }

    private void OnMatchSuccessJson(JsonElement doc)
    {
        if (!TryParseMatchSuccess(doc, out var info))
        {
            _matchSuccessTcs?.TrySetException(new InvalidDataException($"Could not parse MatchSuccess: {doc}"));
            return;
        }

        _matchSuccessTcs?.TrySetResult(info);
    }

    private static bool TryParseMatchSuccess(JsonElement doc, out MatchSuccessInfo info)
    {
        info = null!;
        string ip = TryGetString(doc, "ip", "Ip");
        int port = TryGetInt(doc, "port", "Port");
        int roomId = TryGetInt(doc, "roomID", "roomId", "RoomID");
        int mapId = TryGetInt(doc, "mapID", "mapId", "MapID");
        int gameMode = TryGetInt(doc, "gameMode", "GameMode");

        if (string.IsNullOrEmpty(ip) || port <= 0 || roomId <= 0)
        {
            return false;
        }

        info = new MatchSuccessInfo
        {
            Ip = ip,
            Port = port,
            RoomId = roomId,
            MapId = mapId,
            GameMode = gameMode
        };
        return true;
    }

    private static string TryGetString(JsonElement doc, params string[] names)
    {
        foreach (var name in names)
        {
            if (doc.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static int TryGetInt(JsonElement doc, params string[] names)
    {
        foreach (var name in names)
        {
            if (doc.TryGetProperty(name, out var prop) && prop.TryGetInt32(out int value))
            {
                return value;
            }
        }

        return 0;
    }

    private static Uri BuildHubUri(string lobbyBaseUrl, long uid)
    {
        var apiUri = new Uri(lobbyBaseUrl.EndsWith('/') ? lobbyBaseUrl : lobbyBaseUrl + "/");
        var builder = new UriBuilder(apiUri.Scheme, apiUri.Host, apiUri.Port, "/signalRHub")
        {
            Query = $"uid={uid}"
        };
        return builder.Uri;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
