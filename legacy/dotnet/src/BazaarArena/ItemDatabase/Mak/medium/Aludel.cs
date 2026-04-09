using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

public static class Aludel
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "炼金梨缶",
            Desc = "▶造成 {Poison} 剧毒；每有 1 件相邻的药水或原料，此物品 +{Custom_0} 多重释放",
            Cooldown = 7.0,
            Tags = Tag.Tool,
            Poison = [4, 8, 12, 16],
            Custom_0 = 1,
            Abilities =
            [
                Ability.Poison,
            ],
            Auras =
            [
                new AuraDefinition
                {
                    Attribute = Key.Multicast,
                    Condition = Condition.SameAsCaster,
                    Value = Formula.Caster(Key.Custom_0)
                        * Formula.Count(Condition.AdjacentToCaster & Condition.WithTag(Tag.Potion | Tag.Reagent)),
                },
            ],
        };
    }
}

