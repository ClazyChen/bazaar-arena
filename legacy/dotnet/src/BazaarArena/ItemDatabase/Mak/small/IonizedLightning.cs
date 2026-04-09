using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class IonizedLightning
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "离子闪电",
            Desc = "相邻物品 {+Custom_0%} 暴击率",
            Cooldown = 0.0,
            Tags = Tag.Reagent,
            Custom_0 = [10, 20, 30, 40],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritRate,
                    Condition = Condition.AdjacentToCaster,
                    Value = Formula.Caster(Key.Custom_0),
                    Percent = true,
                },
            ],
        };
    }
}

