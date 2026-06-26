using System.Diagnostics;
using System.Net.Http.Headers;
using FieldStressHarness.Models;
using FieldStressHarness.Protocol;
using ProtoBuf;

namespace FieldStressHarness.Services;

public sealed class LobbyApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _lobbyBaseUrl;

    public LobbyApiClient(string lobbyBaseUrl, TimeSpan? timeout = null)
    {
        _lobbyBaseUrl = lobbyBaseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        };
    }

    public Task<(LoginResponse Response, long ElapsedMs)> LoginAsync(
        AccountEntry account,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var request = new LoginRequest
        {
            ID = account.Id,
            Password = account.Password,
            DeviceID = deviceId
        };

        return PostAsync<LoginRequest, LoginResponse>("Auth/Login", request, auth: null, cancellationToken);
    }

    public async Task<int> PrepareLobbyDataAsync(LoggedInAccount account, CancellationToken cancellationToken = default)
    {
        var characterRequest = new CharacterListRequest { UID = account.Uid };
        var (characterResponse, _) = await PostAsync<CharacterListRequest, CharacterListResponse>(
            "Lobby/GetCharacterList",
            characterRequest,
            account,
            cancellationToken);

        if (!characterResponse.Success || characterResponse.Characters == null || characterResponse.Characters.Count == 0)
        {
            throw new InvalidOperationException(
                $"GetCharacterList failed (success={characterResponse.Success}, count={characterResponse.Characters?.Count ?? 0})");
        }

        var inventoryRequest = new InventoryListRequest { UID = account.Uid };
        var (inventoryResponse, _) = await PostAsync<InventoryListRequest, InventoryListResponse>(
            "Lobby/GetInventoryList",
            inventoryRequest,
            account,
            cancellationToken);

        if (!inventoryResponse.Success || inventoryResponse.Items == null)
        {
            throw new InvalidOperationException($"GetInventoryList failed (success={inventoryResponse.Success})");
        }

        var equippedWeapon = inventoryResponse.Items
            .FirstOrDefault(item => item.IsEquipped && item.Category == ItemCategory.Weapon);

        return equippedWeapon != null ? (int)equippedWeapon.SubCategory : (int)ItemSubCategory.Pistol;
    }

    public Task<(EnqueueMatchingResponse Response, long ElapsedMs)> EnqueueMatchingAsync(
        LoggedInAccount account,
        bool isPve,
        CancellationToken cancellationToken = default)
    {
        string endpoint = isPve ? "Lobby/RequestRegistPveMatching" : "Lobby/RequestRegistMatching";
        var request = new EnqueueMatchingRequest { UID = account.Uid };
        return PostAsync<EnqueueMatchingRequest, EnqueueMatchingResponse>(endpoint, request, account, cancellationToken);
    }

    public Task<(CancelMatchingResponse Response, long ElapsedMs)> CancelMatchingAsync(
        LoggedInAccount account,
        bool isPve,
        CancellationToken cancellationToken = default)
    {
        string endpoint = isPve ? "Lobby/RequestCancelPveMatching" : "Lobby/RequestCancelMatching";
        var request = new CancelMatchingRequest { UID = account.Uid };
        return PostAsync<CancelMatchingRequest, CancelMatchingResponse>(endpoint, request, account, cancellationToken);
    }

    public Task<(UsingIdCheckResponse Response, long ElapsedMs)> UsingIdCheckAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        PostAsync<UsingIdCheckRequest, UsingIdCheckResponse>(
            "Auth/UsingIDCheck",
            new UsingIdCheckRequest { ID = id },
            auth: null,
            cancellationToken);

    public Task<(TestRegisterResponse Response, long ElapsedMs)> TestRegisterAsync(
        string id,
        string password,
        string email,
        CancellationToken cancellationToken = default) =>
        PostAsync<TestRegisterRequest, TestRegisterResponse>(
            "TestAuth/Register",
            new TestRegisterRequest { ID = id, Password = password, Email = email },
            auth: null,
            cancellationToken);

    public Task<(TestEmailAuthResponse Response, long ElapsedMs)> TestEmailAuthAsync(
        string email,
        CancellationToken cancellationToken = default) =>
        PostAsync<TestEmailAuthRequest, TestEmailAuthResponse>(
            "TestAuth/EmailAuthTest",
            new TestEmailAuthRequest { Email = email },
            auth: null,
            cancellationToken);

    private async Task<(TResponse Response, long ElapsedMs)> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest requestData,
        LoggedInAccount? auth,
        CancellationToken cancellationToken)
    {
        byte[] body;
        using (var stream = new MemoryStream())
        {
            Serializer.Serialize(stream, requestData);
            body = stream.ToArray();
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_lobbyBaseUrl}/{endpoint}")
        {
            Content = new ByteArrayContent(body)
        };

        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-protobuf"));

        if (auth != null)
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AuthToken);
            httpRequest.Headers.Add("UID", auth.Uid.ToString());
            httpRequest.Headers.Add("DeviceID", auth.DeviceId);
            httpRequest.Headers.Add("ID", auth.Source.Id);
        }

        var stopwatch = Stopwatch.StartNew();
        using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        stopwatch.Stop();

        var responseBytes = await httpResponse.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"HTTP {(int)httpResponse.StatusCode} for {endpoint} ({stopwatch.ElapsedMilliseconds}ms)");
        }

        TResponse response;
        using (var stream = new MemoryStream(responseBytes))
        {
            response = Serializer.Deserialize<TResponse>(stream)
                ?? throw new InvalidDataException($"Empty response for {endpoint}.");
        }

        return (response, stopwatch.ElapsedMilliseconds);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
