using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

public static class MagicCarpet
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "魔法飞毯",
            Desc = "▶造成 {Damage} 伤害；造成暴击时，此物品的冷却时间缩短 {Custom_2} 秒（限本场战斗）；造成暴击时，此物品开始飞行",
            Cooldown = 7.0,
            Tags = Tag.Weapon | Tag.Vehicle | Tag.Relic,
            Damage = [40, 80, 160, 320],
            Custom_0 = 1000,
            Custom_1 = 1,
            Custom_2 = 1,
            Abilities =
            [
                Ability.Damage,
                Ability.ReduceAttribute(Key.CooldownMs).Override(
                    trigger: Trigger.Crit,
                    condition: Condition.SameAsCaster,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low
                ),
                Ability.StartFlying.Override(
                    trigger: Trigger.Crit,
                    condition: Condition.SameAsCaster,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_1,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }
}

