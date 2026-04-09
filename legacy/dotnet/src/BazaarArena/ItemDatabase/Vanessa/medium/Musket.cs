using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Musket
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "火枪",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；相邻物品触发灼烧时，装填此物品；相邻物品触发灼烧时，此物品伤害提高 {Custom_0}（限本场战斗）",
            Cooldown = 6.0,
            Tags = Tag.Weapon,
            Damage = [100, 150, 200],
            AmmoCap = 1,
            Reload = 99,
            Custom_0 = [25, 50, 75],
            Abilities =
            [
                Ability.Damage,
                Ability.Reload.Override(
                    trigger: Trigger.Burn,
                    additionalCondition: Condition.AdjacentToCaster,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Lowest),
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Burn,
                    additionalCondition: Condition.AdjacentToCaster,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Lowest),
            ],
        };
    }

    public static ItemTemplate Template_S4()
    {
        return new ItemTemplate
        {
            Name = "火枪_S4",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；触发灼烧时，为此物品装填 {Reload} 发弹药",
            Cooldown = 7.0,
            Tags = Tag.Weapon,
            Damage = [100, 150, 200],
            AmmoCap = 1,
            Reload = 1,
            Abilities =
            [
                Ability.Damage,
                Ability.Reload.Override(
                    trigger: Trigger.Burn,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Lowest),
            ],
        };
    }
}

