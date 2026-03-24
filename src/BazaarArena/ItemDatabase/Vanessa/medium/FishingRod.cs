using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>钓鱼竿（Fishing Rod）：海盗中型水系、工具。▶ 加速此物品右侧的水系物品 2 秒。每天开始时获得 1 小型水系物品（局外成长，不实现）。</summary>
public static class FishingRod
{
    /// <summary>钓鱼竿：5s 中 铜 水系 工具；▶ 加速此物品右侧的水系物品 2 秒（H）；每天开始时获得 1 小型水系物品。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "钓鱼竿",
            Desc = "▶ 加速此物品右侧的水系物品 {HasteSeconds} 秒；每天开始时，获得 1 小型水系物品",
            Tags = Tag.Aquatic | Tag.Tool,
            Cooldown = 5.0,
            Haste = 2.0,
            Abilities =
            [
                Ability.Haste.Override(
                    additionalTargetCondition: Condition.RightOfCaster & Condition.WithTag(Tag.Aquatic),
                    priority: AbilityPriority.High
                ),
            ],
        };
    }
}
