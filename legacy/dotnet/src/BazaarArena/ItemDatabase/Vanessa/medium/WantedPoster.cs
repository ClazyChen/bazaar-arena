using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class WantedPoster
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "通缉海报",
            Desc = "击败英雄时，获得 1 经验；如果击败英雄时使用此物品，额外获得 1 经验；己方物品 {+Custom_0%} 暴击率",
            Cooldown = 0.0,
            Tags = 0,
            Custom_0 = [10, 20, 30],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.CritRate,
                    Condition = Condition.SameSide & Condition.CanCrit,
                    Value = Formula.Caster(Key.Custom_0),
                }
            ]
        };
    }
}

