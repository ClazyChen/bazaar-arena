using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>流星索（Bolas）及其历史版本：海盗小型武器、弹药。</summary>
public static class Bolas
{
    /// <summary>流星索（版本 5）：4s 小 铜 武器；▶ 造成 20 » 40 » 60 » 80 伤害；▶ 减速 1 件物品 2 » 3 » 4 » 5 秒；弹药 2 发。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "流星索",
            Desc = "▶ 造成 {Damage} 伤害；▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；弹药：{AmmoCap}",
            Tags = [Tag.Weapon],
            Cooldown = 4.0,
            Damage = [20, 40, 60, 80],
            Slow = [2.0, 3.0, 4.0, 5.0],
            SlowTargetCount = 1,
            AmmoCap = 2,
            Abilities =
            [
                Ability.Damage,
                Ability.Slow,
            ],
        };
    }

    /// <summary>流星索_S1（版本 4）：6s 小 铜 武器；▶ 造成 40 » 60 » 80 » 100 伤害；▶ 减速 1 件物品 2 » 3 » 4 » 5 秒；弹药 2 发。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "流星索_S1",
            Desc = "▶ 造成 {Damage} 伤害；▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒；弹药：{AmmoCap}",
            Tags = [Tag.Weapon],
            Cooldown = 6.0,
            Damage = [40, 60, 80, 100],
            Slow = [2.0, 3.0, 4.0, 5.0],
            SlowTargetCount = 1,
            AmmoCap = 2,
            Abilities =
            [
                Ability.Damage,
                Ability.Slow,
            ],
        };
    }
}
