using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>海底热泉（Volcanic Vents）：海盗中型水系。▶ 造成 3 » 6 » 9 » 12 灼烧；多重释放：3。</summary>
public static class VolcanicVents
{
    /// <summary>海底热泉（最新版）：7s 中 铜 水系；▶ 造成 {Burn} 灼烧；多重释放：{Multicast}。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "海底热泉",
            Desc = "▶ 造成 {Burn} 灼烧；多重释放：{Multicast}",
            Tags = Tag.Aquatic,
            Cooldown = 7.0,
            Burn = [3, 6, 9, 12],
            Multicast = 3,
            Abilities =
            [
                Ability.Burn,
            ],
        };
    }
}
