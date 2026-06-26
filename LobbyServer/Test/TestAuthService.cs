using System.Security.Cryptography;
using LobbyAPI;
using LobbyServer.Helper;
using LobbyServer.Models;
using LobbyServer.Services;

namespace LobbyServer.Test;

public interface ITestAuthService
{
    Task<TestEmailAuthResponse> EmailAuthTestAsync(TestEmailAuthRequest request);
    Task<TestRegisterResponse> RegisterAsync(TestRegisterRequest request);
}

public sealed class TestAuthService : ITestAuthService
{
    private const int EmailTokenExpiryMinutes = 5;

    private readonly IRedisHelper _redis;
    private readonly IAuthService _authService;

    public TestAuthService(IRedisHelper redis, IAuthService authService)
    {
        _redis = redis;
        _authService = authService;
    }

    public async Task<TestEmailAuthResponse> EmailAuthTestAsync(TestEmailAuthRequest request)
    {
        if (!System.Net.Mail.MailAddress.TryCreate(request.Email, out _))
        {
            return new TestEmailAuthResponse { Success = false };
        }

        string authToken = GenerateAuthToken();
        bool stored = await StoreEmailAuthTokenAsync(request.Email, authToken);
        return new TestEmailAuthResponse
        {
            Success = stored,
            AuthToken = stored ? authToken : string.Empty
        };
    }

    public async Task<TestRegisterResponse> RegisterAsync(TestRegisterRequest request)
    {
        if (!System.Net.Mail.MailAddress.TryCreate(request.Email, out _))
        {
            return new TestRegisterResponse
            {
                Success = false,
                State = RegistResponse.ResultState.Unknown,
                Detail = $"Invalid email format: {request.Email}"
            };
        }

        var emailAuth = await EmailAuthTestAsync(new TestEmailAuthRequest { Email = request.Email });
        if (!emailAuth.Success || string.IsNullOrWhiteSpace(emailAuth.AuthToken))
        {
            return new TestRegisterResponse
            {
                Success = false,
                State = RegistResponse.ResultState.Unknown,
                Detail = "EmailAuthTest failed (Redis or invalid email)"
            };
        }

        var registerResult = await _authService.RegisterAsync(new RegistRequest
        {
            ID = request.ID,
            Password = request.Password,
            Email = request.Email,
            EmailAuthToken = emailAuth.AuthToken
        });

        return new TestRegisterResponse
        {
            Success = registerResult.State == RegistResponse.ResultState.Success,
            State = registerResult.State,
            AuthToken = emailAuth.AuthToken,
            Detail = registerResult.State == RegistResponse.ResultState.Success
                ? string.Empty
                : registerResult.State.ToString()
        };
    }

    private async Task<bool> StoreEmailAuthTokenAsync(string email, string authToken)
    {
        string redisKey = $"email:authtoken:{email}";
        var authData = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Token"] = authToken,
            ["RetryCount"] = "0"
        };

        return await _redis.SetHashFieldsAsync(
            redisKey,
            authData,
            expiry: TimeSpan.FromMinutes(EmailTokenExpiryMinutes));
    }

    private static string GenerateAuthToken()
    {
        byte[] randomBytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        string base64 = Convert.ToBase64String(randomBytes);
        return base64.Replace('+', '-').Replace('/', '_').Replace("=", "");
    }
}
