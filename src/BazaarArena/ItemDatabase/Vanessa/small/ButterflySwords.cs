using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>蝶剑（Butterfly Swords）及其历史版本：海盗小型武器；▶ 造成 10 伤害；多重释放。</summary>
public static class ButterflySwords
{
    /// <summary>蝶剑（版本 12，银）：6s 小 银 武器；▶ 造成 10 伤害；多重释放：2 » 3 » 4。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "蝶剑",
            Desc = "▶ 造成 {Damage} 伤害；多重释放：{Multicast}",
            Tags = Tag.Weapon,
            Cooldown = 6.0,
            Damage = 10,
            Multicast = [2, 3, 4],
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }

    /// <summary>蝶剑_S10（版本 10，银）：7 » 6 » 5s 小 银 武器；▶ 造成 10 伤害；多重释放：2 » 3 » 4。</summary>
    public static ItemTemplate Template_S10()
    {
        return new ItemTemplate
        {
            Name = "蝶剑_S10",
            Desc = "▶ 造成 {Damage} 伤害；多重释放：{Multicast}",
            Tags = Tag.Weapon,
            Cooldown = [7.0, 6.0, 5.0],
            Damage = 10,
            Multicast = [2, 3, 4],
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }
}

