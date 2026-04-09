using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

public static class CrowsNest
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "鸦巢",
            Desc = "己方武器 {+Custom_0%} 暴击率；如果只有 1 件武器，其获得吸血且受到减速的持续时间减半",
            Cooldown = 0.0,
            Tags = Tag.Aquatic | Tag.Property,
            Custom_0 = [40, 60, 80],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritRate,
                    Condition = Condition.SameSide & Condition.WithTag(Tag.Weapon) & Condition.CanCrit,
                    Value = Formula.Caster(Key.Custom_0),
                },
                new AuraDefinition
                {
                    Attribute = Key.LifeSteal,
                    Condition = Condition.OnlyWeapon,
                    Value = Formula.Constant(1),
                },
                new AuraDefinition
                {
                    Attribute = Key.PercentSlowReduction,
                    Condition = Condition.OnlyWeapon,
                    Value = Formula.Constant(50),
                },
            ],
        };
    }
}

