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
        public long UID { get; set; } = 0;
        [ProtoMember(2)]
        public int RemainCount { get; set; } = 0;
        [ProtoMember(3)]
        public NicknameChangeResult Result { get; set; }
        [ProtoMember(4)]
        public string ResultNickname { get; set; } = string.Empty;
    }

    [ProtoContract]
    public class Character
    {
        [ProtoMember(1)]
        public long CharacterInstanceID { get; set; } 

        [ProtoMember(2)]
        public int Level { get; set; }
        [ProtoMember(3)]
        public long Exp { get; set; }
    }

    [ProtoContract]
    public class PvpRecord
    {
        [ProtoMember(1)]
        public long UID { get; set; }
        [ProtoMember(2)]
        public int Win { get; set; }
        [ProtoMember(3)]
        public int Total { get; set; }
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

    [ProtoContract]
    public class EnqueueMatchingRequest
    {
        [ProtoMember(1)]
        public long UID { get; set; }
    }

    [ProtoContract]
    public class EnqueueMatchingResponse
    {
        [ProtoMember(1)]
        public bool Success { get; set; }
    }

    [ProtoContract]
    public class CancelMatchingRequest
    {
        [ProtoMember(1)]
        public long UID { get; set; }
    }

    [ProtoContract]
    public class CancelMatchingResponse
    {
        [ProtoMember(1)]
        public bool Success { get; set; }
    }

    [ProtoContract]
    public class FieldServerInfo
    {
        [ProtoMember(1)]
        public string Name { get; set; }
    }

    public static class MatchRewardConstants
    {
        // 경험치
        public const long WinBaseExp = 10;
        public const long LoseBaseExp = 5;
        public const long PerKillExp = 2;

        // 골드
        public const long WinBaseGold = 100;
        public const long LoseBaseGold = 50;
        public const long PerKillGold = 10;
    }

    public static class CharacterLevelConstants
    {
        public const long ExpPerLevel = 100;
    }

    public static class RankingConstants
    {
        public const int TopEntryCount = 10;
        public const int MinPlayCount = 1;

        public const string DisplayKey = "ranking:pvp:display";
        public const string MetaKey = "ranking:meta";
        public const string RefreshLockKey = "lock:ranking:refresh";

        public static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(2);
        public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan RefreshLockExpiry = TimeSpan.FromMinutes(2);
    }

    [ProtoContract]
    public class RankingEntry
    {
        [ProtoMember(1)]
        public int DisplayRank { get; set; }

        [ProtoMember(2)]
        public long UID { get; set; }

        [ProtoMember(3)]
        public string Nickname { get; set; } = string.Empty;

        [ProtoMember(4)]
        public int Win { get; set; }

        [ProtoMember(5)]
        public int Total { get; set; }

        [ProtoMember(6)]
        public decimal WinRate { get; set; }
    }

    [ProtoContract]
    public class RankingListRequest
    {
    }

    [ProtoContract]
    public class RankingListResponse
    {
        [ProtoMember(1)]
        public bool Success { get; set; }

        [ProtoMember(2)]
        public DateTime RefreshedAt { get; set; }

        [ProtoMember(3)]
        public List<RankingEntry> Entries { get; set; } = new();
    }
}