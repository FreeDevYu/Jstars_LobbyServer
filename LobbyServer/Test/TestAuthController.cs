using Microsoft.AspNetCore.Mvc;

namespace LobbyServer.Test;

[ApiController]
[Route("api/[controller]")]
public class TestAuthController : ControllerBase
{
    private readonly ITestAuthService _testAuthService;

    public TestAuthController(ITestAuthService testAuthService)
    {
        _testAuthService = testAuthService;
    }

    [HttpPost("EmailAuthTest")]
    public async Task<IActionResult> EmailAuthTest([FromBody] TestEmailAuthRequest request)
    {
        var result = await _testAuthService.EmailAuthTestAsync(request);
        return Ok(result);
    }

    [HttpPost("Register")]
    public async Task<IActionResult> Register([FromBody] TestRegisterRequest request)
    {
        var result = await _testAuthService.RegisterAsync(request);
        return Ok(result);
    }
}
