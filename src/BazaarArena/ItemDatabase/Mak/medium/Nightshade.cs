using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

public static class Nightshade
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "颠茄",
            Desc = "▶造成 {Poison} 剧毒；触发治疗时，此物品的剧毒提高 {Custom_0}（限本场战斗）；触发生命再生时，此物品的剧毒提高 {Custom_0}（限本场战斗）",
            Cooldown = 6.0,
            Tags = Tag.Reagent,
            Poison = 6,
            Custom_0 = [2, 4, 6, 8],
            Abilities =
            [
                Ability.Poison,
                Ability.AddAttribute(Key.Poison).Override(
                    trigger: Trigger.Heal,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.High
                ),
                Ability.AddAttribute(Key.Poison).Override(
                    trigger: Trigger.Regen,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.High
                ),
            ],
        };
    }
}

