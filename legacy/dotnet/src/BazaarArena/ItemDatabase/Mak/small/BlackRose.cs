using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class BlackRose
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "黑玫瑰",
            Desc = "▶获得 {Regen} 生命再生；触发剧毒时，此物品的生命再生提高 {Custom_0}（限本场战斗）",
            Cooldown = 6.0,
            Tags = 0,
            Regen = 2,
            Custom_0 = [1, 2, 3, 4],
            Abilities =
            [
                Ability.Regen,
                Ability.AddAttribute(Key.Regen).Override(
                    trigger: Trigger.Poison,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }
}

