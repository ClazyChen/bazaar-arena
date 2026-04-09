using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>锁镰（Kusarigama）及其历史版本：海盗小型武器、科技。</summary>
public static class Kusarigama
{
    /// <summary>锁镰（版本 6，银）：5s 小 银 武器 科技；▶ 造成 4 » 8 » 12 伤害；触发减速时，此物品和相邻武器伤害提高 4 » 8 » 12（限本场战斗）；造成暴击时，此物品和相邻武器伤害提高 4 » 8 » 12（限本场战斗）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "锁镰",
            Desc = "▶ 造成 {Damage} 伤害；触发减速时，此物品和相邻武器伤害提高 {Custom_0}（限本场战斗）；造成暴击时，此物品和相邻武器伤害提高 {Custom_0}（限本场战斗）",
            Tags = Tag.Weapon | Tag.Tech,
            Cooldown = 5.0,
            Damage = [4, 8, 12],
            Custom_0 = [4, 8, 12],
            Abilities =
            [
                Ability.Damage,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Slow,
                    targetCondition: Condition.SameAsCaster | (Condition.AdjacentToCaster & Condition.WithTag(Tag.Weapon)),
                    valueKey: Key.Custom_0
                ),
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Crit,
                    targetCondition: Condition.SameAsCaster | (Condition.AdjacentToCaster & Condition.WithTag(Tag.Weapon)),
                    valueKey: Key.Custom_0
                ),
            ],
        };
    }

    /// <summary>锁镰_S4（版本 4，银）：6s 小 银 武器 科技；▶ 造成 4 » 8 » 12 伤害；▶ 减速 1 » 2 » 3 件物品 1 秒；触发减速时，此物品和相邻武器伤害提高 3 » 6 » 9（限本场战斗）；造成暴击时，此物品和相邻武器伤害提高 3 » 6 » 9（限本场战斗）。</summary>
    public static ItemTemplate Template_S4()
    {
        return new ItemTemplate
        {
            Name = "锁镰_S4",
            Desc = "▶ 造成 {Damage} 伤害；▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；触发减速时，此物品和相邻武器伤害提高 {Custom_0}（限本场战斗）；造成暴击时，此物品和相邻武器伤害提高 {Custom_0}（限本场战斗）",
            Tags = Tag.Weapon | Tag.Tech,
            Cooldown = 6.0,
            Damage = [4, 8, 12],
            Slow = 1.0,
            SlowTargetCount = [1, 2, 3],
            Custom_0 = [3, 6, 9],
            Abilities =
            [
                Ability.Damage,
                Ability.Slow,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Slow,
                    targetCondition: Condition.SameAsCaster | (Condition.AdjacentToCaster & Condition.WithTag(Tag.Weapon)),
                    valueKey: Key.Custom_0
                ),
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Crit,
                    targetCondition: Condition.SameAsCaster | (Condition.AdjacentToCaster & Condition.WithTag(Tag.Weapon)),
                    valueKey: Key.Custom_0
                ),
            ],
        };
    }
}

