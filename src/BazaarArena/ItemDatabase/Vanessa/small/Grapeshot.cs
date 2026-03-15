using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>葡萄弹（Grapeshot）及其历史版本：海盗小型武器、弹药。</summary>
public static class Grapeshot
{
    /// <summary>葡萄弹（版本 5，银）：4s 小 银 武器；▶ 造成 15 » 30 » 60 伤害；弹药：{AmmoCap}；使用其他弹药物品时，为此物品装填 1 发（Lst）。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "葡萄弹",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；使用其他弹药物品时，为此物品装填 1 发",
            Tags = [Tag.Weapon],
            Cooldown = 4.0,
            Damage = [15, 30, 60],
            AmmoCap = 2,
            Custom_0 = 1,
            Abilities =
            [
                Ability.Damage,
                Ability.Reload.Override(
                    condition: Condition.SameSide & Condition.WithTag(Tag.Ammo) & Condition.DifferentFromSource,
                    targetCondition: Condition.SameAsSource,
                    priority: AbilityPriority.Lowest
                ),
            ],
        };
    }

    /// <summary>葡萄弹_S1（铜）：4s 小 铜 武器；▶ 造成 20 » 30 » 40 » 50 伤害；弹药：{AmmoCap}；使用其他弹药物品时，为此物品装填 1 发（Lst）。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "葡萄弹_S1",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；使用其他弹药物品时，为此物品装填 1 发",
            Tags = [Tag.Weapon],
            Cooldown = 4.0,
            Damage = [20, 30, 40, 50],
            AmmoCap = 1,
            Custom_0 = 1,
            Abilities =
            [
                Ability.Damage,
                Ability.Reload.Override(
                    condition: Condition.SameSide & Condition.WithTag(Tag.Ammo) & Condition.DifferentFromSource,
                    targetCondition: Condition.SameAsSource,
                    priority: AbilityPriority.Lowest
                ),
            ],
        };
    }
}
