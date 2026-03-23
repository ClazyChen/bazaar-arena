using BazaarArena.BattleSimulator;

namespace BazaarArena.Core;

public sealed partial class BattleContext
{
    public BattleState BattleState { get; set; } = null!;
    /// <summary>
    /// 当前上下文正在判定/施加效果的目标物品。
    /// 光环判定时表示“正在判断是否能接受光环”的物品；
    /// trigger condition 与 effect apply 中默认与 Caster 一致。
    /// </summary>
    public ItemState Item { get; set; } = null!;
    public ItemState Caster { get; set; } = null!;
    /// <summary>引起当前触发的那件物品（如 UseItem 的被使用物、Slow 的施放者）；无单独「原因」时与 <see cref="Caster"/> 一致（如战斗开始、暴击自身）。</summary>
    public ItemState? Source { get; set; }
    public ItemState? InvokeTarget { get; set; }

    public BattleSide Side { get; set; } = null!;
    public BattleSide Opp { get; set; } = null!;
    public int ResolvedValue { get; set; }
    public int CritMultiplier { get; set; } = 1;
    public bool IsCrit { get; set; }
    public int TimeMs { get; set; }
    public IBattleLogSink LogSink { get; set; } = null!;
    public List<ItemState>? ChargeInducedCastQueue { get; set; }
    public List<(int TriggerName, int SideIndex, int ItemIndex)>? EffectAppliedTriggerQueue { get; set; }
    public Formula? TargetCondition { get; set; }
    public string? EffectLogName { get; set; }
    public int? TargetCountKey { get; set; }

    public void Rebind(
        BattleState battleState,
        BattleSide side,
        BattleSide opp,
        ItemState item,
        int resolvedValue,
        int critMultiplier,
        bool isCrit,
        int timeMs,
        IBattleLogSink logSink,
        List<ItemState>? chargeInducedCastQueue,
        List<(int TriggerName, int SideIndex, int ItemIndex)>? effectAppliedTriggerQueue,
        Formula? targetCondition,
        string? effectLogName,
        int? targetCountKey,
        ItemState? invokeTargetItem)
    {
        BattleState = battleState;
        Side = side;
        Opp = opp;
        Item = item;
        Caster = item;
        Source = item;
        InvokeTarget = invokeTargetItem;
        ResolvedValue = resolvedValue;
        CritMultiplier = critMultiplier;
        IsCrit = isCrit;
        TimeMs = timeMs;
        LogSink = logSink;
        ChargeInducedCastQueue = chargeInducedCastQueue;
        EffectAppliedTriggerQueue = effectAppliedTriggerQueue;
        TargetCondition = targetCondition;
        EffectLogName = effectLogName;
        TargetCountKey = targetCountKey;
    }
}
