using LobbyAPI.Models;
using LobbyServer.Helper;
using LobbyServer.Models;


namespace LobbyServer.Services
{
    public interface ILobbyService
    {
        Task<CharacterListResponse> GetCharactersAsync(CharacterListRequest request);
        Task<InventoryListResponse> GetInventoryListAsync(InventoryListRequest request);
        Task<EquipResponse> EquipAsync(EquipRequest request);
        Task<NicknameChangeResponse> NicknameChangeAsync(NicknameChangeRequest request);
        Task<EnqueueMatchingResponse> EnqueueMatchingAsync(EnqueueMatchingRequest request);
        Task<CancelMatchingResponse> CancelMatchingAsync(CancelMatchingRequest request);
    }

    public class LobbyService : ILobbyService
    {
        private readonly ICharacterHelper _characterHelper;
        private readonly IInventoryHelper _inventoryHelper;
        private readonly IMatchingHelper _matchigHelper;
        private readonly IUserHelper _userHelper;

        public LobbyService(ICharacterHelper characterHelper, IInventoryHelper inventoryHelper, IMatchingHelper matchigHelper, IUserHelper userHelper)
        {
            _characterHelper = characterHelper;
            _inventoryHelper = inventoryHelper;
            _matchigHelper = matchigHelper;
            _userHelper = userHelper;
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

        public async Task<InventoryListResponse> GetInventoryListAsync(InventoryListRequest request)
        {
            long uid = request.UID;

            var inventory = await _inventoryHelper.GetInventoryListByUIDAsync(uid);
            if (inventory == null || inventory.Count() < 1)
            {
                return new InventoryListResponse { Success = false, Items = new List<Item>() };
            }

            return new InventoryListResponse { Success = true, Items = inventory };
        }

        //[Write-Back]
        public async Task<EquipResponse> EquipAsync(EquipRequest request)
        {
            long uid = request.UID;
            long itemInstanceID = request.ItemInstanceID;

            (bool success, long equipped, long unequipped) result = await _inventoryHelper.EquipItem(uid, itemInstanceID);


            return new EquipResponse { Success = result.success, EquipItemID = result.equipped, UnEquipItemID = result.unequipped };
        }

        public async Task<NicknameChangeResponse> NicknameChangeAsync(NicknameChangeRequest request)
        {
            NicknameChangeResponse response = await _inventoryHelper.ChangeNickname(request.UID, request.NewNickname, request.ItemInstanceID);

            if (response.Result == NicknameChangeResult.Success)
            {
                await _userHelper.UpdateUserNickname(response.UID, response.ResultNickname);
            }

            return response;
        }

        public async Task<EnqueueMatchingResponse> EnqueueMatchingAsync(EnqueueMatchingRequest request)
        {
            long uid = request.UID;

            bool success = await _matchigHelper.EnqueueMatchingQueue(uid);
            
            return new EnqueueMatchingResponse { Success = success };
        }

        public async Task<CancelMatchingResponse> CancelMatchingAsync(CancelMatchingRequest request)
        {
            await _matchigHelper.CancelMatchingQueue(request.UID);
            return new CancelMatchingResponse { Success = false };
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
