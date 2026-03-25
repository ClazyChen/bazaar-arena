using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class CaptainsWheel
{
    private static Formula HasOtherVehicleOrLarge { get; } =
        Formula.Count(Condition.SameSide & Condition.WithTag(Tag.Vehicle | Tag.Large));

    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "船舵",
            Desc = "▶ 加速相邻物品 {HasteSeconds} 秒；如果有载具或大型物品，此物品的冷却时间减半",
            Cooldown = 5.0,
            Tags = Tag.Aquatic | Tag.Tool,
            Haste = [1.0, 2.0, 3.0],
            HasteTargetCount = 2,
            Abilities =
            [
                Ability.Haste.Override(
                    additionalTargetCondition: Condition.AdjacentToCaster,
                    priority: AbilityPriority.High),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.PercentCooldownReduction,
                    Condition = HasOtherVehicleOrLarge,
                    Value = Formula.Constant(50),
                }
            ],
        };
    }
}

