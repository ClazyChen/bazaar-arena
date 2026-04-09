using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>十手（Jitte）：海盗小型武器；▶ 造成伤害；▶ 减速；触发减速时，此物品伤害提高（限本场战斗）（Low）。</summary>
public static class Jitte
{
    /// <summary>十手（版本 7，银）：5s 小 银 武器；▶ 造成 20 伤害；▶ 减速 1 件物品 1 » 2 » 3 秒；触发减速时，此物品伤害提高 10 » 20 » 30（限本场战斗）（Low）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "十手",
            Desc = "▶ 造成 {Damage} 伤害；▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；触发减速时，此物品伤害提高 {Custom_0}（限本场战斗）",
            Tags = Tag.Weapon,
            Cooldown = 5.0,
            Damage = 20,
            Slow = [1.0, 2.0, 3.0],
            SlowTargetCount = 1,
            Custom_0 = [10, 20, 30],
            Abilities =
            [
                Ability.Damage,
                Ability.Slow,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Slow,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }
}

