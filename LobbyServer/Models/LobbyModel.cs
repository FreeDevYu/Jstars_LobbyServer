using ProtoBuf;

namespace LobbyAPI.Models
{
    [ProtoContract]
    public class CharacterListRequest
    {
        [ProtoMember(1)]
        public long UID { get; set; }
    }

    [ProtoContract]
    public class CharacterListResponse
    {
        [ProtoMember(1)]
        public List<Character> Characters { get; set; }
        [ProtoMember(2)]
        public bool Success { get; set; }
    }

    [ProtoContract]
    public class InventoryListRequest
    {
        [ProtoMember(1)]
        public long UID { get; set; }
    }

    [ProtoContract]
    public class InventoryListResponse
    {
        [ProtoMember(1)]
        public List<Item> Items { get; set; }
        [ProtoMember(2)]
        public bool Success { get; set; }
    }

    [ProtoContract]
    public class EquipRequest
    {
        [ProtoMember(1)]
        public long UID { get; set; }

        [ProtoMember(2)]
        public long ItemInstanceID { get; set; }
    }

    [ProtoContract]
    public class EquipResponse
    {
        [ProtoMember(1)]
        public bool Success { get; set; }
        [ProtoMember(2)]
        public long EquipItemID { get; set; }
        [ProtoMember(3)]
        public long UnEquipItemID { get; set; }
    }

    [ProtoContract]
    public class NicknameChangeRequest
    {
        [ProtoMember(1)]
        public long UID { get; set; }
        [ProtoMember(2)]
        public string NewNickname { get; set; }
        [ProtoMember(3)]
        public long ItemInstanceID { get; set; }
    }

    [ProtoContract]
    public class NicknameChangeResponse
    {
        [ProtoMember(1)]
        public NicknameChangeResult Result { get; set; }
    }

    [ProtoContract]
    public class Character
    {
        [ProtoMember(1)]
        public long CharacterInstanceID { get; set; } 

        [ProtoMember(2)]
        public int Level { get; set; }
        [ProtoMember(3)]
        public int Exp { get; set; }
    }

    [ProtoContract]
    public class Item
    {
        [ProtoMember(1)]
        public long InstanceID { get; set; }
        [ProtoMember(2)]
        public ItemCategory Category { get; set; }
        [ProtoMember(3)]
        public ItemSubCategory SubCategory { get; set; }
        [ProtoMember(4)]
        public int Level { get; set; }
        [ProtoMember(5)]
        public int Count { get; set; }
        [ProtoMember(6)]
        public bool IsEquipped { get; set; }
    }


    [ProtoContract]
    public enum ItemCategory
    {
        None = 0,
        Weapon = 1,
        Armor = 2,
        Using = 3,
        End
    }

    [ProtoContract]
    public enum ItemSubCategory
    {
        None = 0,
        Pistol = 1,
        Shotgun = 2,
        Sniper = 3,
        MachineGun = 4,

        DummyAromr = 101,

        NicknameChangeCoupon = 1001
    }

    [ProtoContract]
    public enum NicknameChangeResult
    {
        None = 0,
        Success = 1,
        FormatFail = 2,
        DuplicateFail = 3,
        NoCoupon = 4
    }
}