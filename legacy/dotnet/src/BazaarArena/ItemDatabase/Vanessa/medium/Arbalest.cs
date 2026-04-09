using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

public static class Arbalest
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "大钢弩",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；触发加速时，此物品伤害提高 {Custom_0}（限本场战斗）",
            Cooldown = 9.0,
            Tags = Tag.Weapon,
            Damage = 100,
            AmmoCap = 1,
            Custom_0 = [50, 75, 100],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Haste,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low),
            ],
        };
    }
}

