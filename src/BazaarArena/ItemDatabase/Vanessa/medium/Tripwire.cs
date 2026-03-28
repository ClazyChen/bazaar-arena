using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>绊索（Tripwire）：海盗中型陷阱；敌方使用物品时使其减速。</summary>
public static class Tripwire
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "绊索",
            Desc = "敌方使用物品时，使其减速 {SlowSeconds} 秒",
            Tags = Tag.Trap,
            Cooldown = 0.0,
            Slow = [1.0, 2.0],
            SlowTargetCount = 1,
            Abilities =
            [
                Ability.Slow.Override(
                    trigger: Trigger.UseOtherItem,
                    condition: Condition.DifferentSide,
                    targetCondition: Condition.SameAsInvokeTarget,
                    priority: AbilityPriority.Medium),
            ],
        };
    }
}
