using LobbyAPI.Models;
using LobbyServer.LobbyService;
using Microsoft.AspNetCore.Mvc;

namespace LobbyServer.Properties
{
    [Route("api/[controller]")]
    [ApiController]
    public class LobbyController : ControllerBase
    {
        private readonly ILobbyService _lobbyService;

        public LobbyController(ILobbyService lobbyService)
        {
            _lobbyService = lobbyService;
        }

        [HttpPost("GetCharacterList")]
        public async Task<IActionResult> GetCharacterList([FromBody] CharacterListRequest request)
        {
            var result = await _lobbyService.GetCharactersAsync(request);

            if (!result.Success) return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("GetGearList")]
        public async Task<IActionResult> GetGearList([FromBody] GearListRequest request)
        {
            var result = await _lobbyService.GetGearsAsync(request);

            if (!result.Success) return BadRequest(result);

            return Ok(result);
        }
    }
}
