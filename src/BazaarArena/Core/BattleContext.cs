using BazaarArena.BattleSimulator;

namespace BazaarArena.Core;

public sealed partial class BattleContext
{
    public BattleState BattleState { get; set; } = null!;
    public bool AllowCastQueueEnqueue { get; set; } = true;
    /// <summary>
    /// 当前上下文正在判定/施加效果的目标物品。
    /// 光环判定时表示“正在判断是否能接受光环”的物品；
    /// trigger condition 与 effect apply 中默认与 Caster 一致。
    /// </summary>
    public ItemState Item { get; set; } = null!;
    public ItemState Caster { get; set; } = null!;
    /// <summary>引起当前触发的那件物品（如 UseItem 的被使用物、Slow 的施放者）；无单独「原因」时与 <see cref="Caster"/> 一致（如战斗开始、暴击自身）。</summary>
    public ItemState Source { get; set; } = null!;
    public ItemState? InvokeTarget { get; set; }

    public BattleSide CurrentSide => BattleState.Side[Caster.SideIndex];
    public BattleSide OppSide => BattleState.Side[1 - Caster.SideIndex];
    public bool IsCritNow => Caster.CritTimeMs == BattleState.TimeMs && Caster.IsCritThisUse;
    public int CurrentCritMultiplier => IsCritNow ? Math.Max(1, Caster.CritDamage / 100) : 1;

    public int GetItemInt(ItemState item, int key)
    {
        int baseValue = (uint)key < (uint)item.Attributes.Length ? item.GetAttribute(key) : 0;
        var sessionTables = BattleState.SessionTables;
        if (sessionTables == null) return baseValue;
        if (!sessionTables.AurasByAttribute.TryGetValue(key, out var auraIds) || auraIds.Count == 0) return baseValue;
        if (key == Key.Tags)
        {
            int tagMask = baseValue;
            var tagCtx = new BattleContext
            {
                BattleState = BattleState,
                InvokeTarget = null,
            };
            foreach (var auraId in auraIds)
            {
                var source = BattleState.GetAuraOwner(auraId);
                var aura = BattleState.GetAura(auraId);
                if (source.Destroyed) continue;
                tagCtx.Caster = source;
                tagCtx.Item = item;
                tagCtx.Source = item;
                if (aura.Condition.Evaluate(tagCtx) == 0) continue;
                if (aura.Value == null) continue;
                int v = aura.Value.Evaluate(tagCtx);
                if (!aura.Percent) tagMask |= v;
            }
            return tagMask;
        }
        int fixedSum = 0;
        int percentSum = 0;
        var auraCtx = new BattleContext
        {
            BattleState = BattleState,
            InvokeTarget = null,
        };
        foreach (var auraId in auraIds)
        {
            var source = BattleState.GetAuraOwner(auraId);
            var aura = BattleState.GetAura(auraId);
            if (source.Destroyed) continue;
            auraCtx.Caster = source;
            auraCtx.Item = item;
            auraCtx.Source = item;
            auraCtx.InvokeTarget = null;
            if (aura.Condition.Evaluate(auraCtx) == 0) continue;
            if (aura.Value == null) continue;
            int v = aura.Value.Evaluate(auraCtx);
            if (aura.Percent) percentSum += v;
            else fixedSum += v;
        }
        return RatioUtil.PercentFloor(baseValue + fixedSum, 100 + percentSum);
    }
}
