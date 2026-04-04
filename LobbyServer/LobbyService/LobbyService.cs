using LobbyAPI;
using LobbyAPI.Models;
using LobbyServer.Repositories;

namespace LobbyServer.LobbyService
{
    public interface ILobbyService
    {
        Task<CharacterListResponse> GetCharactersAsync(CharacterListRequest request);
    }

    public class LobbyService : ILobbyService
    {
        private readonly ICharacterHelper _characterHelper;
        public LobbyService(ICharacterHelper characterHelper)
        {
            _characterHelper = characterHelper;
        }

        public async Task<CharacterListResponse> GetCharactersAsync(CharacterListRequest request)
        {
            long uid = request.UID;

            var characterList = await _characterHelper.GetAllCharactersByUIDAsync(uid);
            if (characterList == null || characterList.Count() < 1)
            {
                // Message = "데이터가 손상되었습니다. 고객센터에 문의해주세요."
                return new CharacterListResponse { Success = false, Characters = new List<Character>() };
            }

            return new CharacterListResponse { Success = true, Characters = characterList };
        }
    }

    public class CharacterListRequest
    {
       public long UID { get; set; }
    }

    public class CharacterListResponse
    {
        public List<Character> Characters { get; set; }
        public bool Success { get; set; }
    }
}
