using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

public static class Fireflies
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "萤火虫",
            Desc = "▶造成 {Burn} 灼烧；触发减速时，此物品的灼烧提高 {Custom_0}（限本场战斗）；触发减速时，此物品开始飞行",
            Cooldown = 8.0,
            Tags = Tag.Friend,
            Burn = 5,
            Custom_0 = [2, 3, 4, 5],
            Custom_1 = 1,
            Abilities =
            [
                Ability.Burn,
                Ability.AddAttribute(Key.Burn).Override(
                    trigger: Trigger.Slow,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0
                ),
                Ability.StartFlying.Override(
                    trigger: Trigger.Slow,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_1
                ),
            ],
        };
    }
}

