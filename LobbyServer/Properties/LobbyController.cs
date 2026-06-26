using LobbyAPI.Models;
using LobbyServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace LobbyServer.Properties
{
    [Route("api/[controller]")]
    [ApiController]
    public class LobbyController : ControllerBase
    {
        private readonly ILobbyService _lobbyService;
        private readonly IRankingService _rankingService;

        public LobbyController(ILobbyService lobbyService, IRankingService rankingService)
        {
            _lobbyService = lobbyService;
            _rankingService = rankingService;
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

        [RedisAuthorize]
        [HttpPost("GetRankingList")]
        public async Task<IActionResult> GetRankingList([FromBody] RankingListRequest request)
        {
            var result = await _rankingService.GetRankingListAsync(request);
            return Ok(result);
        }

        [RedisAuthorize]
        [HttpPost("RequestRegistMatching")]
        public async Task<IActionResult> Enqueue([FromBody] EnqueueMatchingRequest req)
        {
            var result = await _lobbyService.EnqueueMatchingAsync(req);
            return Ok(result);
        }

        [RedisAuthorize]
        [HttpPost("RequestCancelMatching")]
        public async Task<IActionResult> Cancel([FromBody] CancelMatchingRequest req)
        {
            var result = await _lobbyService.CancelMatchingAsync(req);
            return Ok(result);
        }

        [RedisAuthorize]
        [HttpPost("RequestRegistPveMatching")]
        public async Task<IActionResult> EnqueuePve([FromBody] EnqueueMatchingRequest req)
        {
            var result = await _lobbyService.EnqueuePveMatchingAsync(req);
            return Ok(result);
        }

        [RedisAuthorize]
        [HttpPost("RequestCancelPveMatching")]
        public async Task<IActionResult> CancelPve([FromBody] CancelMatchingRequest req)
        {
            var result = await _lobbyService.CancelPveMatchingAsync(req);
            return Ok(result);
        }
    }
}
