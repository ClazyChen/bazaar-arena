using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>打火机（Lighter）：海盗小型工具；▶ 造成 3 » 5 » 7 » 9 灼烧。</summary>
public static class Lighter
{
    /// <summary>打火机（版本 1）：3s 小 铜 工具；▶ 造成 {Burn} 灼烧。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "打火机",
            Desc = "▶ 造成 {Burn} 灼烧",
            Tags = [Tag.Tool],
            Cooldown = 3.0,
            Burn = [3, 5, 7, 9],
            Abilities =
            [
                Ability.Burn,
            ],
        };
    }
}
