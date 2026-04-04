using LobbyServer.AuthService;
using Microsoft.AspNetCore.Mvc;

namespace LobbyServer.Properties
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("UsingIDCheck")]
        public async Task<IActionResult> UsingIDCheck([FromBody] UsingIDRequest request)
        {
            var result = await _authService.UsingIDCheckAsync(request);

            if (!result.Success) return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("EmailAuth")]
        public async Task<IActionResult> EmaiAuth([FromBody] EmailAuthRequest request)
        {
            var result = await _authService.EmailAuthAsync(request);

            if (!result.Success) return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegistRequest request)
        {
            var result = await _authService.RegisterAsync(request);

            if (result.State != RegistResponse.ResultState.Success) return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);

            if (result.State != LoginResponse.ResultState.Success) return Unauthorized(result);

            return Ok(result);
        }

        [RedisAuthorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            var result = await _authService.LogoutAsync(request);

            if (!result.Success) return Unauthorized(result);

            return Ok(result);
        }
    }
}
