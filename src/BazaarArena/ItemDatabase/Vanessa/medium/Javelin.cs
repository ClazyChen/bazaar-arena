using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Javelin
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "标枪",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；此物品暴击时，加速其他物品 {HasteSeconds} 秒；此物品被装填时，暴击率提高 {Custom_0%}（限本场战斗）",
            Cooldown = 4.0,
            Tags = Tag.Weapon,
            Damage = [50, 100, 150],
            AmmoCap = 2,
            Haste = 2.0,
            Custom_0 = 50,
            Abilities =
            [
                Ability.Damage,
                Ability.Haste.Override(
                    trigger: Trigger.Crit,
                    condition: Condition.SameAsCaster,
                    additionalTargetCondition: Condition.DifferentFromCaster,
                    priority: AbilityPriority.High),
                Ability.AddAttribute(Key.CritRate).Override(
                    trigger: Trigger.Reload,
                    additionalCondition: Condition.InvokeTargetSameAsCaster,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Lowest),
            ],
        };
    }

    public static ItemTemplate Template_S11()
    {
        return new ItemTemplate
        {
            Name = "标枪_S11",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；此物品被装填时，此物品伤害提高 {Custom_0}（限本场战斗）",
            Cooldown = 5.0,
            Tags = Tag.Weapon,
            Damage = [120, 180, 240],
            AmmoCap = 2,
            Custom_0 = [40, 60, 80],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Reload,
                    additionalCondition: Condition.InvokeTargetSameAsCaster,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Lowest),
            ],
        };
    }

    public static ItemTemplate Template_S5()
    {
        return new ItemTemplate
        {
            Name = "标枪_S5",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；此物品被加速时，装填此物品",
            Cooldown = 5.0,
            Tags = Tag.Weapon,
            Damage = [120, 180, 240],
            AmmoCap = 2,
            Reload = 1,
            Abilities =
            [
                Ability.Damage,
                Ability.Reload.Override(
                    trigger: Trigger.Haste,
                    additionalCondition: Condition.InvokeTargetSameAsCaster,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Lowest),
            ],
        };
    }
}

