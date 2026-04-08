using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Medium;

/// <summary>产药药水：暂不实现（仅占位）。</summary>
public static class PotionPotion
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "产药药水",
            Desc = "▶转化为 2 件来自任意英雄的小型药水（限本场战斗）（暂不实现）",
            Cooldown = 2.0,
            Tags = Tag.Potion,
            Abilities = [],
            Auras = [],
        };
    }
}

