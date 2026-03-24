using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>左轮手枪（Revolver）及其历史版本：海盗小型武器、弹药。</summary>
public static class Revolver
{
    /// <summary>左轮手枪（版本 5）：3s 小 铜 武器；▶ 造成 8 » 16 » 24 » 32 伤害；弹药 6 发。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "左轮手枪",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}",
            Tags = Tag.Weapon,
            Cooldown = 3.0,
            Damage = [8, 16, 24, 32],
            AmmoCap = 6,
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }

    /// <summary>左轮手枪_S1（版本 1）：4s 小 铜 武器；▶ 造成 8 » 16 » 24 » 32 伤害；弹药 6 发；暴击率 20%；造成暴击时为此物品装填 2 发弹药（Low）。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "左轮手枪_S1",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；暴击率 {CritRate}%；造成暴击时，为此物品装填 {Custom_0} 发弹药",
            Tags = Tag.Weapon,
            Cooldown = 4.0,
            Damage = [8, 16, 24, 32],
            AmmoCap = 6,
            CritRate = 20,
            Custom_0 = 2,
            Abilities =
            [
                Ability.Damage,
                Ability.Reload.Override(
                    trigger: Trigger.Crit,
                    targetCondition: Condition.SameAsCaster,
                    priority: AbilityPriority.Low
                ),
            ],
        };
    }
}
