using LobbyAPI.Models;
using LobbyServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace LobbyServer.Properties
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShopController : ControllerBase
    {
        private readonly IShopService _shopService;

        public ShopController(IShopService shopService)
        {
            _shopService = shopService;
        }

        [RedisAuthorize]
        [HttpPost("PurchaseProduct")]
        public async Task<IActionResult> PurchaseProduct([FromBody] ShopPurchaseRequest request)
        {
            ShopPurchaseResponse result = await _shopService.PurchaseProductAsync(request);
            return Ok(result);
        }
    }
}
