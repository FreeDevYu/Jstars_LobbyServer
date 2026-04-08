using LobbyServer.AuthService;
using LobbyServer.Models;
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

            //if (!result.Success) return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("EmailAuth")]
        public async Task<IActionResult> EmaiAuth([FromBody] EmailAuthRequest request)
        {
            var result = await _authService.EmailAuthAsync(request);

            return Ok(result);
        }

        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] RegistRequest request)
        {
            var result = await _authService.RegisterAsync(request);
            return Ok(result);
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);

            return Ok(result);
        }

        [RedisAuthorize]
        [HttpPost("Logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            var result = await _authService.LogoutAsync(request);

            return Ok(result);
        }
    }
}
