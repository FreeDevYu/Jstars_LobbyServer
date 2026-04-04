using LobbyAPI;
using LobbyServer.Repositories;

namespace LobbyServer.LobbyService
{
    public interface IGearHelper
    {

    }

    public class GearHelper : IGearHelper
    {
        private readonly ILobbyRespository _lobbyRespository;
        private readonly IRedisHelper _redisHelper;

        public GearHelper(IRedisHelper redisHelper, ILobbyRespository lobbyRespository)
        {

        }


    }
}
