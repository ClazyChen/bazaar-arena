using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

/// <summary>冰山（Iceberg）：海盗大型水系、地产；敌方使用物品时冻结该物品。</summary>
public static class Iceberg
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "冰山",
            Desc = "敌方使用物品时，使其冻结 {FreezeSeconds} 秒",
            Tags = Tag.Aquatic | Tag.Property,
            Cooldown = 0.0,
            Freeze = 1.0,
            FreezeTargetCount = 1,
            Abilities =
            [
                Ability.Freeze.Override(
                    trigger: Trigger.UseOtherItem,
                    condition: Condition.DifferentSide,
                    targetCondition: Condition.SameAsInvokeTarget,
                    priority: AbilityPriority.Medium),
            ],
        };
    }
}
