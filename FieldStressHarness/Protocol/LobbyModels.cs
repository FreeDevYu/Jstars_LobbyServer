using ProtoBuf;

namespace FieldStressHarness.Protocol;

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
    public List<LobbyCharacter> Characters { get; set; } = new();

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
    public List<LobbyItem> Items { get; set; } = new();

    [ProtoMember(2)]
    public bool Success { get; set; }
}

[ProtoContract]
public class LobbyCharacter
{
    [ProtoMember(1)]
    public long CharacterInstanceID { get; set; }

    [ProtoMember(2)]
    public int Level { get; set; }

    [ProtoMember(3)]
    public long Exp { get; set; }
}

[ProtoContract]
public class LobbyItem
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
    End = 4
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
