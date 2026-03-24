using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>手雷（Grenade）：海盗小型武器、弹药；▶ 造成 50 » 100 » 150 » 200 伤害；弹药 1 发；暴击率 25%。</summary>
public static class Grenade
{
    /// <summary>手雷：5s 小 铜 武器；▶ 造成 50 » 100 » 150 » 200 伤害；弹药：{AmmoCap}；暴击率 {CritRate}%。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "手雷",
            Desc = "▶ 造成 {Damage} 伤害；弹药：{AmmoCap}；暴击率 {CritRate}%",
            Tags = [Tag.Weapon],
            Cooldown = 5.0,
            Damage = [50, 100, 150, 200],
            AmmoCap = 1,
            CritRate = 25,
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }
}
