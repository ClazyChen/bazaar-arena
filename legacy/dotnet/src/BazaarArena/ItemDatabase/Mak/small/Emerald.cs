using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class Emerald
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "翡翠",
            Desc = "▶造成 {Poison} 剧毒；其他剧毒物品 +{Custom_0} 剧毒",
            Cooldown = 7.0,
            Tags = Tag.Relic,
            Poison = [2, 3, 4, 5],
            Custom_0 = [2, 3, 4, 5],
            Abilities =
            [
                Ability.Poison,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Poison,
                    Condition = Condition.SameSide & Condition.DifferentFromCaster & Condition.WithDerivedTag(DerivedTag.Poison),
                    Value = Formula.Caster(Key.Custom_0),
                },
            ],
        };
    }
}

