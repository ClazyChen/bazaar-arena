using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class Ruby
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "红宝石",
            Desc = "▶造成 {Burn} 灼烧；其他灼烧物品 +{Custom_0} 灼烧",
            Cooldown = 7.0,
            Tags = Tag.Relic,
            Burn = [2, 3, 4, 5],
            Custom_0 = [2, 3, 4, 5],
            Abilities =
            [
                Ability.Burn,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Burn,
                    Condition = Condition.SameSide & Condition.DifferentFromCaster & Condition.WithDerivedTag(DerivedTag.Burn),
                    Value = Formula.Caster(Key.Custom_0),
                },
            ],
        };
    }
}

