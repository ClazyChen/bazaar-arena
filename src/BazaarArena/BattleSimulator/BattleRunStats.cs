using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>单次对战统计结果：胜方、时长、每物品统计、强度曲线。</summary>
public class BattleRunStats
{
    /// <summary>胜方：0 或 1，-1 表示平局。</summary>
    public int Winner { get; set; }

    /// <summary>对战结束时的毫秒数。</summary>
    public int DurationMs { get; set; }

    /// <summary>是否平局。</summary>
    public bool IsDraw => Winner < 0;

    /// <summary>每侧每物品的统计。</summary>
    public List<ItemStatRow> ItemStats { get; set; } = [];

    /// <summary>强度曲线：玩家0 按时间采样的累计量。</summary>
    public List<StrengthCurvePoint> StrengthCurveSide0 { get; set; } = [];

    /// <summary>强度曲线：玩家1 按时间采样的累计量。</summary>
    public List<StrengthCurvePoint> StrengthCurveSide1 { get; set; } = [];
}

/// <summary>单侧单个物品的统计行。</summary>
public class ItemStatRow
{
    public int SideIndex { get; set; }
    /// <summary>显示用玩家编号（1 或 2）。</summary>
    public int Player => SideIndex + 1;
    public string ItemName { get; set; } = "";
    /// <summary>本物品在本次对战中使用的档位（用于在 UI 中按实际等级展示 Tooltip）。</summary>
    public ItemTier Tier { get; set; }
    public int CastCount { get; set; }
    public int Damage { get; set; }
    public int Burn { get; set; }
    public int Poison { get; set; }
    public int Shield { get; set; }
    public int Heal { get; set; }
    public int Regen { get; set; }
}

/// <summary>强度曲线上的一个时间点：到该时刻为止的累计量（一侧）。</summary>
public class StrengthCurvePoint
{
    public int TimeMs { get; set; }
    /// <summary>该侧造成的伤害累计。</summary>
    public int Damage { get; set; }
    /// <summary>该侧施加的灼烧累计。</summary>
    public int Burn { get; set; }
    /// <summary>该侧施加的剧毒累计。</summary>
    public int Poison { get; set; }
    /// <summary>该侧获得的护盾累计。</summary>
    public int Shield { get; set; }
    /// <summary>该侧治疗累计。</summary>
    public int Heal { get; set; }
    /// <summary>该侧生命再生累计。</summary>
    public int Regen { get; set; }
    /// <summary>该时刻该侧当前生命值（纵轴为当前生命值时使用）。</summary>
    public int Hp { get; set; }
}
