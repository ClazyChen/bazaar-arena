using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>抓钩（Grappling Hook）及其历史版本：海盗小型武器、工具。</summary>
public static class GrapplingHook
{
    /// <summary>抓钩（版本 3）：7s 小 铜 武器 工具；▶ 造成 20 » 40 » 60 » 80 伤害；▶ 减速 1 » 2 » 3 » 4 件物品 1 秒。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "抓钩",
            Desc = "▶ 造成 {Damage} 伤害；▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒",
            Tags = [Tag.Weapon, Tag.Tool],
            Cooldown = 7.0,
            Damage = [20, 40, 60, 80],
            Slow = 1.0,
            SlowTargetCount = [1, 2, 3, 4],
            Abilities =
            [
                Ability.Damage,
                Ability.Slow,
            ],
        };
    }

    /// <summary>抓钩_S1（版本 1）：7s 小 铜 武器 工具；▶ 造成 12 » 18 » 24 » 32 伤害；▶ 减速 1 件物品 2 » 3 » 4 » 5 秒。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "抓钩_S1",
            Desc = "▶ 造成 {Damage} 伤害；▶ 减速 {SlowTargetCount} 件物品 {SlowSeconds} 秒",
            Tags = [Tag.Weapon, Tag.Tool],
            Cooldown = 7.0,
            Damage = [12, 18, 24, 32],
            Slow = [2.0, 3.0, 4.0, 5.0],
            SlowTargetCount = 1,
            Abilities =
            [
                Ability.Damage,
                Ability.Slow,
            ],
        };
    }
}
