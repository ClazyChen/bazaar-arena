using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class CyberSai
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "赛博铁尺",
            Desc = "造成 {Damage} 伤害；造成暴击时，1 件武器的暴击率提高 {+Custom_0%}（限本场战斗）；武器暴击率提高时，该武器伤害提高 {Custom_1}（限本场战斗）",
            Cooldown = 3.0,
            Tags = Tag.Weapon | Tag.Tech,
            Damage = [10, 20, 30],
            Custom_0 = [5, 10, 15],
            Custom_1 = [5, 10, 15],
            ModifyAttributeTargetCount = 1,
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.CritRate).Override(
                    trigger: Trigger.Crit,
                    additionalTargetCondition: Condition.WithTag(Tag.Weapon),
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low),
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.CritRateIncreased,
                    additionalCondition: Condition.InvokeTargetWithTag(Tag.Weapon),
                    targetCondition: Condition.SameAsInvokeTarget,
                    valueKey: Key.Custom_1,
                    priority: AbilityPriority.Low),
            ],
        };
    }

    public static ItemTemplate Template_S6()
    {
        return new ItemTemplate
        {
            Name = "赛博铁尺_S6",
            Desc = "造成 {Damage} 伤害；每场战斗前 {Custom_0} 次造成暴击时，你获得无敌 {InvincibleSeconds} 秒",
            Cooldown = 3.0,
            Tags = Tag.Weapon | Tag.Tech,
            Damage = [10, 20, 30, 40],
            Custom_0 = [2, 3, 4, 5],
            Custom_1 = 1,
            Invincible = 1.0,
            Abilities =
            [
                Ability.Damage,
                Ability.Invincible.Override(
                    trigger: Trigger.Crit,
                    additionalCondition: Formula.Caster(Key.Custom_0),
                    priority: AbilityPriority.Low),
                Ability.ReduceAttribute(Key.Custom_0).Override(
                    trigger: Trigger.Crit,
                    additionalCondition: Formula.Caster(Key.Custom_0),
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_1,
                    effectLogName: "",
                    priority: AbilityPriority.Immediate),
            ],
        };
    }
}

