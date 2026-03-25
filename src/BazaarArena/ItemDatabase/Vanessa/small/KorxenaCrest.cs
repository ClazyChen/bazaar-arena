using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

public static class KorxenaCrest
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "蔻森娜纹章",
            Desc = "己方物品 {+Custom_0%} 暴击率",
            Cooldown = 0.0,
            Tags = Tag.Apparel | Tag.Relic,
            Custom_0 = [15, 25, 35],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritRate,
                    Condition = Condition.SameSide & Condition.CanCrit,
                    Value = Formula.Caster(Key.Custom_0),
                }
            ],
        };
    }
}

