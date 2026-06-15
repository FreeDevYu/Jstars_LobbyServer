using ProtoBuf;

namespace LobbyAPI.Models
{
    [ProtoContract]
    public class ShopPurchaseRequest
    {
        [ProtoMember(1)]
        public long UID { get; set; }
        [ProtoMember(2)]
        public long ProductId { get; set; }
    }

    [ProtoContract]
    public class ShopPurchaseResponse
    {
        [ProtoMember(1)]
        public ShopPurchaseResult Result { get; set; }
        [ProtoMember(2)]
        public long RemainGold { get; set; }
        [ProtoMember(3)]
        public Item RewardItem { get; set; }
    }

    [ProtoContract]
    public enum ShopPurchaseResult
    {
        Fail = 0,
        Success = 1,
        ProductUnavailable = 2,
        InsufficientGold = 3,
        UserUnavailable = 4,
        UnsupportedCurrency = 5
    }
}

namespace LobbyServer.Models
{
    public class ShopPurchaseProcedureResult
    {
        public int Result { get; set; }
        public long RemainGold { get; set; }
        public long RewardInstanceId { get; set; }
        public int RewardCategory { get; set; }
        public int RewardSubCategory { get; set; }
        public int RewardLevel { get; set; }
        public int RewardCount { get; set; }
    }
}
