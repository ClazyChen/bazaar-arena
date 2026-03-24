using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>沙滩充气球（Beach Ball）：海盗中型水系、玩具。▶ 加速 2 » 3 » 4 » 5 件水系或玩具物品 2 秒。</summary>
public static class BeachBall
{
    /// <summary>沙滩充气球：5s 中 铜 水系 玩具；▶ 加速 2 » 3 » 4 » 5 件水系或玩具物品 2 秒（H）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "沙滩充气球",
            Desc = "▶ 加速 {HasteTargetCount} 件水系或玩具物品 {HasteSeconds} 秒",
            Tags = Tag.Aquatic | Tag.Toy,
            Cooldown = 5.0,
            Haste = 2.0,
            HasteTargetCount = [2, 3, 4, 5],
            Abilities =
            [
                Ability.Haste.Override(
                    additionalTargetCondition: Condition.WithTag(Tag.Aquatic) | Condition.WithTag(Tag.Toy),
                    priority: AbilityPriority.High
                ),
            ],
        };
    }
}
