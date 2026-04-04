using LobbyAPI.Models;
using LobbyServer.Models;


namespace LobbyServer.LobbyService
{
    public interface ILobbyService
    {
        Task<CharacterListResponse> GetCharactersAsync(CharacterListRequest request);
        Task<GearListResponse> GetGearsAsync(GearListRequest request);
    }

    public class LobbyService : ILobbyService
    {
        private readonly ICharacterHelper _characterHelper;
        private readonly IGearHelper _gearHelper;

        public LobbyService(ICharacterHelper characterHelper, IGearHelper gearHelper)
        {
            _characterHelper = characterHelper;
            _gearHelper = gearHelper;
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

        public async Task<GearListResponse> GetGearsAsync(GearListRequest request)
        {
            long characterID = request.characterID;
            var gearList = await _gearHelper.GetAllGearsByUIDAsync(characterID);
            if (gearList == null || gearList.Count() < 1)
            {
                return new GearListResponse { Success = false, Gears = new List<Gear>() };
            }

            return new GearListResponse { Success = true, Gears = gearList };
        }
    }
}

//할일
/*
 1. 장비리스트 전달.
 2. 닉네임변경요청
 -> 클라이언트와 로비서버 연결필요
 3. pvp 게임룸생성 -> 로비서버가 필드서버와 연락필요
 4. pve 진행요청
 5. 
 */
