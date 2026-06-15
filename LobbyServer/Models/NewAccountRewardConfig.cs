using LobbyAPI.Models;

namespace LobbyServer.Models
{
    /// <summary>
    /// 신규 계정 생성 시 DB/Redis에 지급할 아이템 한 건.
    /// </summary>
    public record NewAccountItemReward(
        ItemCategory Category,
        ItemSubCategory SubCategory,
        int Level = 1,
        int Count = 1,
        bool EquipOnGrant = false);

    /// <summary>
    /// 신규 계정 보상 설정. 값만 수정하면 지급 내용을 변경할 수 있습니다.
    /// CreateAccount SP의 inventory INSERT는 제거 예정 — 현재는 C# 지급을 사용합니다.
    /// </summary>
    public static class NewAccountRewardConfig
    {
        public const long StarterGold = 1000;

        public static IReadOnlyList<NewAccountItemReward> StarterItems { get; } = new[]
        {
            new NewAccountItemReward(ItemCategory.Weapon, ItemSubCategory.Pistol, Level: 1, Count: 1, EquipOnGrant: true),
            new NewAccountItemReward(ItemCategory.Using, ItemSubCategory.NicknameChangeCoupon, Level: 0, Count: 1),
        };
    }
}
