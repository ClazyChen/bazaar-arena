using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class FungalSpores
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "真菌孢子",
            Desc = "▶剧毒物品的剧毒提高 {Custom_0}（限本场战斗）",
            Cooldown = 5.0,
            Tags = 0,
            Custom_0 = [2, 3, 4, 5],
            Abilities =
            [
                Ability.AddAttribute(Key.Poison).Override(
                    valueKey: Key.Custom_0,
                    additionalTargetCondition: Condition.WithDerivedTag(DerivedTag.Poison)
                ),
            ],
        };
    }
}

