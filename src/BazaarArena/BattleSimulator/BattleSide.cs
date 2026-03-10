using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>单方玩家战斗状态：生命、护盾、灼烧、剧毒、生命再生与物品列表。</summary>
public class BattleSide
{
    public int MaxHp { get; set; }
    public int Hp { get; set; }
    public int Shield { get; set; }
    public int Burn { get; set; }
    public int Poison { get; set; }
    public int Regen { get; set; }
    public List<BattleItemState> Items { get; set; } = [];

    /// <summary>战斗内按光环上下文读取物品属性；凡有 (side, itemIndex) 的战斗内读属性应走此入口。</summary>
    public int GetItemInt(int itemIndex, string key, int defaultValue = 0)
    {
        var item = Items[itemIndex];
        return item.Template.GetInt(key, item.Tier, defaultValue, new BattleAuraContext(this, itemIndex));
    }
}
