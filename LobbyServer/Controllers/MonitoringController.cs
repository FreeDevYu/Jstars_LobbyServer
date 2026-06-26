using LobbyServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace LobbyServer.Controllers
{
    [ApiController]
    [Route("api/monitor")]
    public class MonitoringController : ControllerBase
    {
        private readonly IMonitoringService _monitoringService;
        private readonly IConfiguration _configuration;

        public MonitoringController(IMonitoringService monitoringService, IConfiguration configuration)
        {
            _monitoringService = monitoringService;
            _configuration = configuration;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(
            [FromHeader(Name = "X-Monitor-Key")] string? headerKey,
            [FromQuery] string? key)
        {
            if (!IsAuthorized(headerKey ?? key))
                return Unauthorized();

            var summary = await _monitoringService.GetSummaryAsync();
            return Ok(summary);
        }

        private bool IsAuthorized(string? providedKey)
        {
            var configuredKey = _configuration["Monitoring:ApiKey"];
            if (string.IsNullOrWhiteSpace(configuredKey))
                return true;

            return string.Equals(providedKey, configuredKey, StringComparison.Ordinal);
        }
    }
}
