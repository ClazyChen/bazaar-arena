using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>战斗中单件物品的运行时状态。</summary>
public class BattleItemState
{
    /// <summary>本物品在本次战斗中的阵营下标（0 或 1），由 Run 初始化时设置。</summary>
    public int SideIndex { get; set; }
    /// <summary>本物品在己方物品列表中的下标，由 Run 初始化时设置。</summary>
    public int ItemIndex { get; set; }

    public ItemTemplate Template { get; set; }
    public ItemTier Tier { get; set; }

    /// <summary>已过的冷却时间（毫秒）。</summary>
    public int CooldownElapsedMs { get; set; }

    public int HasteRemainingMs { get; set; }
    public int SlowRemainingMs { get; set; }
    public int FreezeRemainingMs { get; set; }
    /// <summary>是否处于飞行状态；战斗开始为 false，「开始飞行」/「结束飞行」效果可修改。</summary>
    public bool InFlight { get; set; }
    public bool Destroyed { get; set; }
    public int AmmoRemaining { get; set; }

    /// <summary>每个能力上次触发的时间（毫秒），用于 250ms 间隔。</summary>
    public List<int> LastTriggerMsByAbility { get; set; } = [];

    public BattleItemState(ItemTemplate template, ItemTier tier)
    {
        Template = template;
        Tier = tier;
        AmmoRemaining = template.GetInt(nameof(ItemTemplate.AmmoCap), tier);
        for (int i = 0; i < template.Abilities.Count; i++)
            LastTriggerMsByAbility.Add(-1000);
    }
}
