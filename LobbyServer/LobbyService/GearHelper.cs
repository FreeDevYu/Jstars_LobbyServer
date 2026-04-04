using LobbyAPI;
using LobbyAPI.Models;
using LobbyServer.Repositories;
using System.Text.Json;

namespace LobbyServer.LobbyService
{
    public interface IGearHelper
    {
        Task<List<Gear>> GetAllGearsByUIDAsync(long uid);
    }

    public class GearHelper : IGearHelper
    {
        private readonly ILobbyRespository _lobbyRespository;
        private readonly IRedisHelper _redisHelper;
        private readonly TimeSpan _gearDataExpiry = TimeSpan.FromHours(2);

        public GearHelper(IRedisHelper redisHelper, ILobbyRespository lobbyRespository)
        {
            _lobbyRespository = lobbyRespository;
            _redisHelper = redisHelper;
        }

        public async Task<List<Gear>> GetAllGearsByUIDAsync(long uid)
        {
            List<Gear> result = null;
            string redisKey = $"gears:{uid}";

            var cachingData = await _redisHelper.GetAllHashFieldsAsync(redisKey);

            if (cachingData != null && cachingData.Count > 0)
            {
                result = cachingData.Values
                                  .Select(json => JsonSerializer.Deserialize<Gear>(json))
                                  .ToList();
                return result;
            }

            var dbData = await _lobbyRespository.GetGearsByCharacterIDAsync(uid);
            result = dbData.ToList();

            if (result.Count > 0)
            {
                var hashEntries = new Dictionary<string, string>();

                foreach (var gear in result)
                {
                    string field = gear.InstanceID.ToString();
                    string jsonValue = JsonSerializer.Serialize(gear);

                    hashEntries.Add(field, jsonValue);
                }

                // for문 밖에서 단 한 번만 통신하여 Redis에 모든 캐릭터를 일괄 저장 (네트워크 최적화)
                await _redisHelper.SetHashFieldsAsync(redisKey, hashEntries, _gearDataExpiry);
            }

            return result;
        }

    }
}
