using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

public static class WaterWheel
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "水车",
            Desc = "▶ 加速其他物品 {HasteSeconds} 秒（限本场战斗）；使用相邻物品时，为此物品充能 {ChargeSeconds} 秒",
            Cooldown = [8.0, 7.0, 6.0],
            Tags = Tag.Aquatic | Tag.Property,
            Haste = 2.0,
            Charge = 2.0,
            Abilities =
            [
                Ability.Haste.Override(
                    additionalTargetCondition: Condition.DifferentFromCaster,
                    priority: AbilityPriority.High),
                Ability.Charge.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.AdjacentToCaster,
                    targetCondition: Condition.SameAsCaster),
            ],
        };
    }
}

