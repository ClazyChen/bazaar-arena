using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>盐钳海盗（Old Saltclaw）：海盗小型武器、水系、伙伴；▶ 造成伤害；触发加速/减速时，此物品伤害提高（限本场战斗）。</summary>
public static class OldSaltclaw
{
    /// <summary>盐钳海盗（版本 8，银）：6s 小 银 武器 水系 伙伴；▶ 造成 30 伤害；触发加速/减速时，此物品伤害提高 5 » 10 » 15（High）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "盐钳海盗",
            Desc = "▶ 造成 {Damage} 伤害；触发加速时，此物品伤害提高 {Custom_0}（限本场战斗）；触发减速时，此物品伤害提高 {Custom_0}（限本场战斗）",
            Tags = Tag.Weapon | Tag.Aquatic | Tag.Friend,
            Cooldown = 6.0,
            Damage = 30,
            Custom_0 = [5, 10, 15],
            LifeSteal = 0,
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Haste,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.High
                ),
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Slow,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_0,
                    priority: AbilityPriority.High
                ),
            ],
        };
    }
}

