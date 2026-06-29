using LobbyAPI;
using Microsoft.AspNetCore.SignalR;

namespace LobbyServer.Hubs
{
    public class SignalRHub : Hub
    {
        private readonly IRedisHelper _redisHelper;

        public SignalRHub(IRedisHelper redisHelper)
        {
            _redisHelper = redisHelper;
        }

        public override async Task OnConnectedAsync()
        {
            var uid = Context.GetHttpContext()?.Request.Query["uid"].ToString();
            if (string.IsNullOrWhiteSpace(uid) || !long.TryParse(uid, out _))
            {
                Context.Abort();
                return;
            }

            string? previousConnectionId = await _redisHelper.GetValueAsync($"SignalRConn:{uid}");
            if (!string.IsNullOrEmpty(previousConnectionId) &&
                !string.Equals(previousConnectionId, Context.ConnectionId, StringComparison.Ordinal))
            {
                await _redisHelper.DeleteKeyAsync($"ConnToUid:{previousConnectionId}");
            }

            await _redisHelper.SetKeyValueAsync($"SignalRConn:{uid}", Context.ConnectionId);
            await _redisHelper.SetKeyValueAsync($"ConnToUid:{Context.ConnectionId}", uid);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            string uidStr = await _redisHelper.GetValueAsync($"ConnToUid:{Context.ConnectionId}");

            if (long.TryParse(uidStr, out long uid))
            {
                await _redisHelper.SetKeyValueAsync($"UserStatus:{uid}", "Offline");
                await _redisHelper.DeleteKeyAsync($"SignalRConn:{uid}");
                await _redisHelper.DeleteKeyAsync($"ConnToUid:{Context.ConnectionId}");
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}