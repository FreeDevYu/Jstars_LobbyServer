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

        [RedisAuthorize]
        [HttpPost("GetCharacterList")]
        public async Task<IActionResult> GetCharacterList([FromBody] CharacterListRequest request)
        {
            var result = await _lobbyService.GetCharactersAsync(request);

            return Ok(result);
        }

        [RedisAuthorize]
        [HttpPost("GetInventoryList")]
        public async Task<IActionResult> GetInventoryList([FromBody] InventoryListRequest request)
        {
            var result = await _lobbyService.GetInventoryListAsync(request);

            return Ok(result);
        }

        [RedisAuthorize]
        [HttpPost("NicknameChange")]
        public async Task<IActionResult> NicknameChange([FromBody] NicknameChangeRequest request)
        {
            var result = await _lobbyService.NicknameChangeAsync(request);

            return Ok(result);
        }

        [RedisAuthorize]
        [HttpPost("EquipRequest")]
        public async Task<IActionResult> EquipRequest([FromBody] EquipRequest request)
        {
            var result = await _lobbyService.EquipAsync(request);

            return Ok(result);
        }
    }
}
