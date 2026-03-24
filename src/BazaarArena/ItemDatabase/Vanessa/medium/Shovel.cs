using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Vanessa.Medium;

/// <summary>铲子（Shovel）：海盗中型武器、工具。▶ 造成伤害。每天开始时获得 1 来自任意英雄的小型物品（局外成长，不实现）。</summary>
public static class Shovel
{
    /// <summary>铲子（最新版）：5s 中 铜 武器 工具；▶ 造成 20 » 40 » 60 » 70 伤害；每天开始时，获得 1 来自任意英雄的小型物品。</summary>
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "铲子",
            Desc = "▶ 造成 {Damage} 伤害；每天开始时，获得 1 来自任意英雄的小型物品",
            Tags = Tag.Weapon | Tag.Tool,
            Cooldown = 5.0,
            Damage = [20, 40, 60, 70],
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }

    /// <summary>铲子_S1（历史版）：10s 中 铜 武器 工具；▶ 造成 50 » 75 » 100 » 125 伤害；每天开始时，获得 1 来自任意英雄的小型物品。</summary>
    public static ItemTemplate Template_S1()
    {
        return new ItemTemplate
        {
            Name = "铲子_S1",
            Desc = "▶ 造成 {Damage} 伤害；每天开始时，获得 1 来自任意英雄的小型物品",
            Tags = Tag.Weapon | Tag.Tool,
            Cooldown = 10.0,
            Damage = [50, 75, 100, 125],
            Abilities =
            [
                Ability.Damage,
            ],
        };
    }
}
