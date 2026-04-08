using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

public static class Refractor
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "折射棱镜",
            Desc =
                "▶造成 {Damage} 伤害；触发减速时，此物品的伤害提高 {Custom_0}（限本场战斗）；触发冻结时，此物品的伤害提高 {Custom_0}（限本场战斗）；触发灼烧时，此物品的伤害提高 {Custom_0}（限本场战斗）；触发剧毒时，此物品的伤害提高 {Custom_0}（限本场战斗）",
            Cooldown = 6.0,
            Tags = Tag.Weapon,
            Damage = 20,
            Custom_0 = [10, 20, 30, 40],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Slow,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low
                ),
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Freeze,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0
                ),
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Burn,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0
                ),
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Poison,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0
                ),
            ],
        };
    }
}

