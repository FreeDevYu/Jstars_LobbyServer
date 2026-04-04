using LobbyAPI.Models;
using SqlKata.Execution;

namespace LobbyServer.Repositories
{
    public interface ILobbyRespository
    {
        Task<IEnumerable<Character>> GetAllCharactersByUIDAsync(long uid);
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
    }
}
