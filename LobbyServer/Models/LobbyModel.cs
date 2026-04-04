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
    public class GearListRequest
    {
        [ProtoMember(1)]
        public long characterID { get; set; }
    }

    [ProtoContract]
    public class GearListResponse
    {
        [ProtoMember(1)]
        public List<Gear> Gears { get; set; }
        [ProtoMember(2)]
        public bool Success { get; set; }
    }

    [ProtoContract]
    public class Character
    {
        [ProtoMember(1)]
        public long CharacterInstanceID { get; set; } 
        [ProtoMember(2)]
        public int HeroTypeId { get; set; }
        [ProtoMember(3)]
        public int Level { get; set; }
        [ProtoMember(4)]
        public int Exp { get; set; }
    }

    [ProtoContract]
    public class Gear
    {
        [ProtoMember(1)]
        public long InstanceID { get; set; }
        [ProtoMember(2)]
        public GearType Type { get; set; }
        [ProtoMember(3)]
        public GearInstanceType TypeInstance { get; set; }
        [ProtoMember(4)]
        public int Enchant { get; set; }
        [ProtoMember(5)]
        public bool IsEquipped { get; set; }
    }

    [ProtoContract]
    public enum GearType
    {
        None = 0,
        Weapon = 1,
        Armor = 2,
        End
    }

    [ProtoContract]
    public enum GearInstanceType
    {
        Pistol = 1,
        Shotgun = 2,
        Sniper = 3,
        MachineGun = 4,

        DummyAromr = 101
    }
}