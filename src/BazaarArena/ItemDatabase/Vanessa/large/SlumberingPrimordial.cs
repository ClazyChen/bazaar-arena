using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Large;

/// <summary>沉眠元初体（Slumbering Primordial）：海盗大型武器；多重释放 4；灼烧/剧毒/冻结时提高伤害并充能。</summary>
public static class SlumberingPrimordial
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "沉眠元初体",
            Desc = "▶ 造成 {Damage} 伤害；多重释放：{Multicast}；触发灼烧时，此物品伤害提高 {Custom_0}（限本场战斗）；触发剧毒时，此物品伤害提高 {Custom_0}（限本场战斗）；触发冻结时，此物品伤害提高 {Custom_0}（限本场战斗）；触发灼烧、剧毒或冻结时，为此物品充能 {ChargeSeconds} 秒",
            Tags = Tag.Weapon,
            Cooldown = 25.0,
            Damage = [20, 25],
            Multicast = 4,
            Custom_0 = [20, 25],
            Charge = [1.0, 2.0],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Burn,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low),
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Poison,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low),
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Freeze,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low),
                Ability.Charge.Override(
                    trigger: Trigger.Burn,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Low).Also(
                    trigger: Trigger.Poison).Also(
                    trigger: Trigger.Freeze),
            ],
        };
    }
}
