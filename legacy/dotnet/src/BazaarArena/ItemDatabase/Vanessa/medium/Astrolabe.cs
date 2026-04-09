using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Astrolabe
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "星盘",
            Desc = "▶ 加速 {HasteTargetCount} 件物品 {HasteSeconds} 秒；使用其他非武器物品时，为此物品充能 {ChargeSeconds} 秒",
            Cooldown = 6.0,
            Tags = Tag.Tool,
            Haste = 1.0,
            HasteTargetCount = [2, 3, 4],
            Charge = 1.0,
            Abilities =
            [
                Ability.Haste.Override(priority: AbilityPriority.Low),
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: ~Condition.WithTag(Tag.Weapon),
                    targetCondition: Condition.SameAsCaster),
            ],
        };
    }

    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "星盘_S1",
            Desc = "▶ 加速 1 件非武器物品 {HasteSeconds} 秒；使用其他非武器物品时，为此物品充能 {ChargeSeconds} 秒",
            Cooldown = [7.0, 6.0, 5.0],
            Tags = Tag.Tool,
            Haste = 1.0,
            Charge = 1.0,
            Abilities =
            [
                Ability.Haste.Override(
                    additionalTargetCondition: ~Condition.WithTag(Tag.Weapon),
                    priority: AbilityPriority.Low),
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: ~Condition.WithTag(Tag.Weapon),
                    targetCondition: Condition.SameAsCaster),
            ],
        };
    }
}

