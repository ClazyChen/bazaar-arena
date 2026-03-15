using BazaarArena.BattleSimulator;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Small;

/// <summary>独角鲸（Narwhal）及其历史版本：海盗小型武器、水系、伙伴。</summary>
public static class Narwhal
{
    /// <summary>独角鲸（最新，对应表格版本 7）：3s 小 铜 武器 水系 伙伴；▶ 造成 5 » 10 » 15 » 20 伤害。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "独角鲸",
            Desc = "▶ 造成 {Damage} 伤害",
            Tags = [Tag.Weapon, Tag.Aquatic, Tag.Friend],
            Cooldown = 3.0,
            Damage = [5, 10, 15, 20],
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }

    /// <summary>独角鲸_S1（旧版，对应表格版本 1）：4s 小 铜 武器 水系 伙伴；▶ 造成 10 » 20 » 30 » 40 伤害。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "独角鲸_S1",
            Desc = "▶ 造成 {Damage} 伤害",
            Tags = [Tag.Weapon, Tag.Aquatic, Tag.Friend],
            Cooldown = 4.0,
            Damage = [10, 20, 30, 40],
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }
}
