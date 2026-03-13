namespace BazaarArena.Core;

/// <summary>能力定义：触发器名、优先级与效果应用（Apply）。触发间隔 5 帧（250ms）由模拟器维护。</summary>
public class AbilityDefinition
{
    /// <summary>单条触发配置：一套 Trigger/Condition/SourceCondition/InvokeTargetCondition 组合。</summary>
    public sealed class TriggerEntry
    {
        /// <summary>触发器名字，如「使用物品」。</summary>
        public string TriggerName { get; set; } = "";

        /// <summary>引起触发的物品需满足的条件；语义同 AbilityDefinition.Condition。</summary>
        public Condition? Condition { get; set; }

        /// <summary>能力持有者需满足的条件；语义同 AbilityDefinition.SourceCondition。</summary>
        public Condition? SourceCondition { get; set; }

        /// <summary>触发器所指向的物品需满足的条件；语义同 AbilityDefinition.InvokeTargetCondition。</summary>
        public Condition? InvokeTargetCondition { get; set; }
    }

    /// <summary>多套触发配置；为空或空列表时按旧字段（TriggerName/Condition/...）行为。</summary>
    public List<TriggerEntry>? Triggers { get; set; }

    /// <summary>触发器名字，如「使用物品」。</summary>
    public string TriggerName { get; set; } = "";

    /// <summary>能力优先级；默认 Medium，仅非默认时需在定义中显式指定。</summary>
    public AbilityPriority Priority { get; set; } = AbilityPriority.Medium;

    /// <summary>引起触发的物品（ConditionContext.Item，如「被使用的物品」）需满足的条件。可选，null 时由触发器决定默认（UseItem → SameAsSource，其他 → SameSide）。评估时 Source=能力持有者、Item=引起触发的物品。</summary>
    public Condition? Condition { get; set; }

    /// <summary>能力持有者需满足的条件；评估时 Item=Source=能力持有者，故可用 WithTag(tag)、InFlight 等表达「本物品带某 tag / 在飞行」等。</summary>
    public Condition? SourceCondition { get; set; }

    /// <summary>触发器所指向的物品（ConditionContext.Item）需满足的条件（如 Slow 时被减速的物品）。默认 null 表示不限制。评估时 Source=能力持有者、Item=指向的物品。</summary>
    public Condition? InvokeTargetCondition { get; set; }

    /// <summary>多目标效果（冻结/减速/充能/加速/摧毁）的目标选择条件；评估时 Source=能力持有者、Item=候选目标。如不设则由效果默认（DifferentSide 或 SameSide）。</summary>
    public Condition? TargetCondition { get; set; }

    /// <summary>固定数值；当 ValueKey 对应模板字段为 0 时使用。</summary>
    public int Value { get; set; }

    /// <summary>模板字段名，如 "Damage"、"Custom_0"；用 template.GetInt(ValueKey, tier) 结算数值，未设时由 Apply 委托内用 GetResolvedValue(key) 取值。</summary>
    public string? ValueKey { get; set; }

    /// <summary>是否对基础数值乘暴击倍率；Charge/Freeze/Slow 等为 false。</summary>
    public bool ApplyCritMultiplier { get; set; } = true;

    /// <summary>Trigger 为 UseItem 且未在 Override 中提供过 condition（仅用 additionalCondition 时）为 true，表示「自己使用」；Override 时若传入了 condition 则设为 false。仅 UseSelf 的 UseItem 能力可参与暴击判定。</summary>
    public bool UseSelf { get; set; } = true;

    /// <summary>效果应用委托；由 Core/Effect 预定义或自定义效果设置。</summary>
    public Action<IEffectApplyContext>? Apply { get; set; }

    /// <summary>确保 Triggers 至少包含一条与主字段（TriggerName/Condition/...）一致的配置。</summary>
    internal void SyncPrimaryTriggerEntryFromTopLevel()
    {
        Triggers ??= new List<TriggerEntry>();
        if (Triggers.Count == 0)
            Triggers.Add(new TriggerEntry());

        var primary = Triggers[0];
        primary.TriggerName = TriggerName;
        primary.Condition = Condition;
        primary.SourceCondition = SourceCondition;
        primary.InvokeTargetCondition = InvokeTargetCondition;
    }

    /// <summary>若尚未初始化 Triggers，则根据主字段创建首条 TriggerEntry；不覆盖已有配置。</summary>
    internal void EnsureTriggersInitializedFromTopLevel()
    {
        if (Triggers != null && Triggers.Count > 0) return;
        SyncPrimaryTriggerEntryFromTopLevel();
    }

    /// <summary>解析数值：ValueKey 或 defaultKey 对应 template.GetInt；结果为 0 时用 Value。</summary>
    public int ResolveValue(ItemTemplate template, ItemTier tier, string defaultKey)
    {
        string key = !string.IsNullOrEmpty(ValueKey) ? ValueKey : defaultKey;
        int v = template.GetInt(key, tier);
        return v != 0 ? v : Value;
    }

    /// <summary>就地覆盖部分属性并返回 this，仅用于 Ability.xxx.Override(...) 链式调用。仅当参数非 null 时覆盖；Condition 与 TargetCondition 按「当前默认 + 传入参数」合并。</summary>
    public AbilityDefinition Override(
        string? trigger = null,
        AbilityPriority? priority = null,
        Condition? condition = null,
        Condition? additionalCondition = null,
        Condition? sourceCondition = null,
        Condition? invokeTargetCondition = null,
        Condition? targetCondition = null,
        Condition? additionalTargetCondition = null,
        string? valueKey = null,
        int? value = null,
        bool? applyCritMultiplier = null,
        Action<IEffectApplyContext>? apply = null)
    {
        string originalTrigger = TriggerName;
        if (trigger != null) TriggerName = trigger;
        if (priority != null) Priority = priority.Value;
        if (sourceCondition != null) SourceCondition = sourceCondition;
        if (invokeTargetCondition != null) InvokeTargetCondition = invokeTargetCondition;
        if (valueKey != null) ValueKey = valueKey;
        if (value != null) Value = value.Value;
        if (applyCritMultiplier != null) ApplyCritMultiplier = applyCritMultiplier.Value;
        if (apply != null) Apply = apply;

        if (condition != null || additionalCondition != null)
        {
            if (condition != null || TriggerName != Trigger.UseItem)
                UseSelf = false;
            var defaultCond = TriggerName == Trigger.UseItem ? Condition.SameAsSource : Condition.SameSide;
            var baseCond = condition ?? Condition ?? defaultCond;
            Condition = additionalCondition != null ? (baseCond & additionalCondition) : baseCond;
        }
        else if (trigger != null && originalTrigger != TriggerName)
        {
            // 仅修改 Trigger 且未传入 condition 时，直接按新 Trigger 设置默认条件：
            // UseItem → SameAsSource，其余 → SameSide。
            Condition = TriggerName == Trigger.UseItem ? Condition.SameAsSource : Condition.SameSide;
        }
        if (targetCondition != null || additionalTargetCondition != null)
        {
            var baseTarget = targetCondition ?? TargetCondition;
            TargetCondition = baseTarget != null
                ? (additionalTargetCondition != null ? (baseTarget & additionalTargetCondition) : baseTarget)
                : additionalTargetCondition;
        }
        SyncPrimaryTriggerEntryFromTopLevel();
        return this;
    }

    /// <summary>在现有能力基础上追加一套触发配置，返回 this 以便链式调用。</summary>
    public AbilityDefinition Also(
        string trigger,
        Condition? condition = null,
        Condition? additionalCondition = null,
        Condition? sourceCondition = null,
        Condition? invokeTargetCondition = null)
    {
        EnsureTriggersInitializedFromTopLevel();

        Condition? defaultCond = null;
        if (trigger == Trigger.UseItem)
            defaultCond = Condition.SameAsSource;
        else if (trigger == Trigger.Freeze || trigger == Trigger.Slow || trigger == Trigger.Crit || trigger == Trigger.Destroy)
            defaultCond = Condition.SameSide;
        else if (trigger == Trigger.BattleStart)
            defaultCond = Condition.Always;

        var baseCond = condition ?? defaultCond;
        Condition? mergedCondition;
        if (additionalCondition != null)
        {
            mergedCondition = baseCond != null ? (baseCond & additionalCondition) : additionalCondition;
        }
        else
        {
            mergedCondition = baseCond;
        }

        Triggers!.Add(new TriggerEntry
        {
            TriggerName = trigger,
            Condition = mergedCondition,
            SourceCondition = sourceCondition ?? SourceCondition,
            InvokeTargetCondition = invokeTargetCondition ?? InvokeTargetCondition,
        });

        return this;
    }
}
