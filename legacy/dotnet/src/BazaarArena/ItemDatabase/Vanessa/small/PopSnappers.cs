using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>燃烧响炮（Pop Snappers）：海盗小型玩具、弹药；▶ 造成 4 » 6 » 8 » 10 灼烧；弹药 4 发。</summary>
public static class PopSnappers
{
    /// <summary>燃烧响炮：3s 小 铜 玩具；▶ 造成 {Burn} 灼烧；弹药：{AmmoCap}。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "燃烧响炮",
            Desc = "▶ 造成 {Burn} 灼烧；弹药：{AmmoCap}",
            Tags = Tag.Toy,
            Cooldown = 3.0,
            Burn = [4, 6, 8, 10],
            AmmoCap = 4,
            Abilities =
            [
                Ability.Burn,
            ],
        };
    }
}
