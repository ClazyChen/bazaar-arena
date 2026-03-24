namespace BazaarArena.Core;

public class AbilityDefinition
{
    private static int s_nextAbilityId = 1;

    public AbilityDefinition()
    {
        AbilityId = System.Threading.Interlocked.Increment(ref s_nextAbilityId);
    }

    public int AbilityId { get; }
    public List<TriggerEntry> TriggerEntries { get; set; } = [];
    public AbilityPriority Priority { get; set; } = AbilityPriority.Medium;
    public AbilityType AbilityType { get; set; } = AbilityType.None;
    public Action<BattleContext, AbilityDefinition>? Apply { get; set; }
    public Formula? TargetCondition { get; set; }
    public int? ValueKey { get; set; }
    public bool ApplyCritMultiplier { get; set; } = true;
    public string? EffectLogName { get; set; }
    public int? TargetCountKey { get; set; }

    private static Formula DefaultConditionByTrigger(int trigger) =>
        trigger == Trigger.UseItem
            ? Condition.SameAsCaster
            : trigger == Trigger.UseOtherItem
                ? (Condition.SameSide & Condition.DifferentFromCaster)
                : Condition.SameSide;

    private static Formula DefaultTargetConditionByTrigger(int trigger) =>
        trigger == Trigger.UseItem
            ? Condition.SameAsCaster
            : Condition.SameSide;

    public AbilityDefinition Override(
        int? trigger = null,
        AbilityPriority? priority = null,
        Formula? condition = null,
        Formula? additionalCondition = null,
        Formula? targetCondition = null,
        Formula? additionalTargetCondition = null,
        int? valueKey = null,
        bool? applyCritMultiplier = null,
        Action<BattleContext, AbilityDefinition>? apply = null,
        string? effectLogName = null,
        int? targetCountKey = null)
    {
        if (TriggerEntries.Count == 0)
            TriggerEntries.Add(new TriggerEntry { Trigger = Trigger.UseItem, Condition = Condition.SameAsCaster });

        if (trigger != null)
        {
            TriggerEntries[0].Trigger = trigger.Value;
            TriggerEntries[0].Condition = DefaultConditionByTrigger(trigger.Value);
            TargetCondition = DefaultTargetConditionByTrigger(trigger.Value);
        }
        if (priority != null) Priority = priority.Value;
        if (valueKey != null) ValueKey = valueKey.Value;
        if (applyCritMultiplier != null) ApplyCritMultiplier = applyCritMultiplier.Value;
        if (apply != null) Apply = apply;
        if (effectLogName != null) EffectLogName = effectLogName;
        if (targetCountKey != null) TargetCountKey = targetCountKey.Value;

        if (condition != null || additionalCondition != null)
        {
            var baseCond = condition ?? TriggerEntries[0].Condition ?? DefaultConditionByTrigger(TriggerEntries[0].Trigger);
            TriggerEntries[0].Condition = additionalCondition != null ? (baseCond & additionalCondition) : baseCond;
        }

        if (targetCondition != null || additionalTargetCondition != null)
        {
            var baseTarget = targetCondition ?? TargetCondition;
            TargetCondition = baseTarget != null
                ? (additionalTargetCondition != null ? (baseTarget & additionalTargetCondition) : baseTarget)
                : additionalTargetCondition;
        }

        return this;
    }

    public AbilityDefinition Also(
        int trigger,
        Formula? condition = null,
        Formula? additionalCondition = null)
    {
        var baseCond = condition ?? DefaultConditionByTrigger(trigger);
        var merged = additionalCondition != null ? (baseCond & additionalCondition) : baseCond;
        TriggerEntries.Add(new TriggerEntry
        {
            Trigger = trigger,
            Condition = merged,
        });
        return this;
    }
}
