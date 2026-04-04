using LobbyAPI.Models;
using LobbyServer.Models;
using SqlKata.Execution;

namespace LobbyServer.Repositories
{
    public interface ILobbyRespository
    {
        Task<IEnumerable<Character>> GetAllCharactersByUIDAsync(long uid);
        Task<IEnumerable<Gear>> GetGearsByCharacterIDAsync(long uidcharacterID);
    }

    public class LobbyRespository : ILobbyRespository
    {
        private readonly QueryFactory _db;

        public LobbyRespository(QueryFactory db)
        {
            _db = db;
        }

        public async Task<IEnumerable<Character>> GetAllCharactersByUIDAsync(long uid)
        {
            return await _db.Query("characters")
                    .Where("uid", uid)
                    .Select(
                        "character_instance_id AS CharacterInstanceID",
                        "hero_type_id AS HeroTypeId",
                        "level AS Level",
                        "exp AS Exp"
                    )
                    .GetAsync<Character>();
        }

        public async Task<IEnumerable<Gear>> GetGearsByCharacterIDAsync(long characterID)
        {
            return await _db.Query("character_gears")
                  .Where("character_instance_id", characterID)
                  .Select(
                      "gear_instance_id AS InstanceID",
                      "gear_type_id AS Type",
                      "gear_type_instance_id AS TypeInstance",
                      "gear_enchant AS Enchant",
                      "is_equipped AS IsEquipped"
                  )
                  .GetAsync<Gear>();
        }
    }
}
