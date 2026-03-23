using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>皮皮虾（Mantis Shrimp）：海盗小型武器、水系、伙伴、弹药；▶ 造成 20 伤害、2 灼烧；弹药 2 发；触发减速时，此物品伤害提高 10 » 15 » 20 » 25、灼烧提高 2 » 3 » 4 » 5（限本场战斗）。</summary>
public static class MantisShrimp
{
    /// <summary>皮皮虾（最新版）：9s 小 铜 武器 水系 伙伴；▶ 造成 {Damage} 伤害、{Burn} 灼烧；弹药：{AmmoCap}；触发减速时，此物品伤害提高 {Custom_0}、灼烧提高 {Custom_1}（限本场战斗）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "皮皮虾",
            Desc = "▶ 造成 {Damage} 伤害、{Burn} 灼烧；弹药：{AmmoCap}；触发减速时，此物品伤害提高 {Custom_0}；触发减速时，灼烧提高 {Custom_1}（限本场战斗）",
            Tags = [Tag.Weapon, Tag.Aquatic, Tag.Friend],
            Cooldown = 9.0,
            Damage = 20,
            Burn = 2,
            AmmoCap = 2,
            Custom_0 = [10, 15, 20, 25],
            Custom_1 = [2, 3, 4, 5],
            Abilities =
            [
                Ability.Damage,
                Ability.Burn,
                Ability.AddAttribute(Key.Damage).Override(
                    trigger: Trigger.Slow,
                    targetCondition: Condition.SameAsCaster
                ),
                Ability.AddAttribute(Key.Burn).Override(
                    trigger: Trigger.Slow,
                    targetCondition: Condition.SameAsCaster,
                    valueKey: Key.Custom_1
                ),
            ],
        };
    }
}
