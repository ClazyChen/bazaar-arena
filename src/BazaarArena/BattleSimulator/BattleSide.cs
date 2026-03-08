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
}
