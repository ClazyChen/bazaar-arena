using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class Moss
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "青苔",
            Desc = "▶相邻生命再生物品的生命再生提高 {Custom_0}（限本场战斗）",
            Cooldown = 5.0,
            Tags = Tag.Reagent,
            Custom_0 = [1, 2, 3, 4],
            Abilities =
            [
                Ability.AddAttribute(Key.Regen).Override(
                    valueKey: Key.Custom_0,
                    additionalTargetCondition: Condition.AdjacentToCaster & Condition.WithDerivedTag(DerivedTag.Regen),
                    priority: AbilityPriority.High
                ),
            ],
        };
    }
}

