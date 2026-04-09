using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

public static class IncendiaryRounds
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "燃烧子弹",
            Desc = "使用相邻物品时，造成 {Burn} 灼烧",
            Cooldown = 0.0,
            Burn = [1, 2, 3],
            Abilities =
            [
                Ability.Burn.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.AdjacentToCaster),
            ],
        };
    }

    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "燃烧子弹_S1",
            Desc = "使用相邻物品时，造成 {Burn} 灼烧；相邻弹药物品 +1 最大弹药量",
            Cooldown = 0.0,
            Burn = [1, 2, 3],
            Abilities =
            [
                Ability.Burn.Override(
                    trigger: Trigger.UseOtherItem,
                    additionalCondition: Condition.AdjacentToCaster),
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.AmmoCap,
                    Condition = Condition.AdjacentToCaster & Condition.WithDerivedTag(DerivedTag.Ammo),
                    Value = Formula.Constant(1),
                }
            ],
        };
    }
}

