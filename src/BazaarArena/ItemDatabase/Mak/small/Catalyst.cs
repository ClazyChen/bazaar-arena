using BazaarArena.Core;

namespace BazaarArena.ItemDatabase.Mak.Small;

/// <summary>催化剂：暂不实现（仅占位）。</summary>
public static class Catalyst
{
    public static ItemTemplate Template()
    {
        return new ItemTemplate
        {
            Name = "催化剂",
            Desc = "【局外】出售此物品时，转化你最左侧的物品（暂不实现）",
            Cooldown = 0.0,
            Tags = Tag.Loot,
            Abilities = [],
            Auras = [],
        };
    }
}

