using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

public sealed class BattleState
{
    private const int AbilityStridePerIndex = 32;
    private const int AuraStridePerIndex = 32;
    private const int SideStridePerItem = 2;

    public BattleSide[] Side { get; } = new BattleSide[2];
    public BattleSessionTables? SessionTables { get; set; }
    public Dictionary<int, AbilityState> AbilityStates { get; } = [];
    public int TimeMs { get; set; }
    public IBattleLogSink LogSink { get; set; } = null!;
    public List<ItemState> CastQueue { get; } = [];
    public static int BuildAbilityId(int sideId, int itemId, int abilityIndex) =>
        abilityIndex * AbilityStridePerIndex + itemId * SideStridePerItem + sideId;

    public AbilityDefinition GetAbility(int abilityId)
    {
        DecodeAbilityId(abilityId, out int sideId, out int itemId, out int abilityIndex);
        return Side[sideId].Items[itemId].Template.Abilities[abilityIndex];
    }

    public ItemState GetAbilityOwner(int abilityId)
    {
        DecodeAbilityId(abilityId, out int sideId, out int itemId, out _);
        return Side[sideId].Items[itemId];
    }

    public static int BuildAuraId(int sideId, int itemId, int auraIndex) =>
        auraIndex * AuraStridePerIndex + itemId * SideStridePerItem + sideId;

    public AuraDefinition GetAura(int auraId)
    {
        DecodeAuraId(auraId, out int sideId, out int itemId, out int auraIndex);
        return Side[sideId].Items[itemId].Template.Auras[auraIndex];
    }

    public ItemState GetAuraOwner(int auraId)
    {
        DecodeAuraId(auraId, out int sideId, out int itemId, out _);
        return Side[sideId].Items[itemId];
    }

    private static void DecodeAbilityId(int abilityId, out int sideId, out int itemId, out int abilityIndex)
    {
        sideId = abilityId & 1;
        abilityIndex = abilityId / AbilityStridePerIndex;
        itemId = (abilityId % AbilityStridePerIndex) / SideStridePerItem;
    }

    private static void DecodeAuraId(int auraId, out int sideId, out int itemId, out int auraIndex)
    {
        sideId = auraId & 1;
        auraIndex = auraId / AuraStridePerIndex;
        itemId = (auraId % AuraStridePerIndex) / SideStridePerItem;
    }

    internal AbilityQueueBuckets CurrentAbilityBuckets { get; private set; } = new();
    internal AbilityQueueBuckets NextAbilityBuckets { get; private set; } = new();

    internal void SwapAbilityBuckets() =>
        (CurrentAbilityBuckets, NextAbilityBuckets) = (NextAbilityBuckets, CurrentAbilityBuckets);

    public void InvokeTrigger(
        int triggerName,
        ItemState? causeItem,
        ItemState? invokeTargetItem,
        int? invokeCount = null,
        Action<int, ItemState?>? executeImmediate = null) =>
        InvokeTrigger(triggerName, causeItem, invokeTargetItem, null, invokeCount, executeImmediate);

    public void InvokeTriggerMany(
        int triggerName,
        ItemState? causeItem,
        IReadOnlyList<ItemState> invokeTargetItems,
        int? invokeCount = null,
        Action<int, ItemState?>? executeImmediate = null) =>
        InvokeTrigger(triggerName, causeItem, null, invokeTargetItems, invokeCount, executeImmediate);

    internal void InvokeTrigger(
        int triggerName,
        ItemState? causeItem,
        ItemState? invokeTargetItem,
        IReadOnlyList<ItemState>? invokeTargetItems,
        int? invokeCount,
        Action<int, ItemState?>? executeImmediate)
    {
        if (SessionTables == null)
            return;
        var current = CurrentAbilityBuckets;
        var next = NextAbilityBuckets;
        int pendingCount = Math.Max(0, invokeCount ?? 1);
        var triggerCtx = new BattleContext
        {
            BattleState = this,
        };
        var triggerAbilities = SessionTables.AbilitiesByTrigger[triggerName];
        foreach (int abilityId in triggerAbilities)
        {
            var abilityOwner = GetAbilityOwner(abilityId);
            if (abilityOwner.Destroyed) continue;
            var ability = GetAbility(abilityId);
            int matchedTargetCount = 0;
            List<ItemState>? matchedInvokeTargets = null;
            if (invokeTargetItems != null && invokeTargetItems.Count > 0)
            {
                for (int ti = 0; ti < invokeTargetItems.Count; ti++)
                {
                    var target = invokeTargetItems[ti];
                    bool matchedOneTarget = false;
                    foreach (var entry in ability.TriggerEntries)
                    {
                        if (entry.Trigger != triggerName) continue;
                        triggerCtx.Item = causeItem ?? abilityOwner;
                        triggerCtx.Caster = abilityOwner;
                        triggerCtx.Source = causeItem ?? abilityOwner;
                        triggerCtx.InvokeTarget = target;
                        if (entry.Condition.Evaluate(triggerCtx) == 0) continue;
                        matchedOneTarget = true;
                        break;
                    }
                    if (!matchedOneTarget) continue;
                    matchedInvokeTargets ??= new List<ItemState>(invokeTargetItems.Count);
                    matchedInvokeTargets.Add(target);
                    matchedTargetCount++;
                }
            }
            else
            {
                foreach (var entry in ability.TriggerEntries)
                {
                    if (entry.Trigger != triggerName) continue;
                    triggerCtx.Item = causeItem ?? abilityOwner;
                    triggerCtx.Caster = abilityOwner;
                    triggerCtx.Source = causeItem ?? abilityOwner;
                    triggerCtx.InvokeTarget = invokeTargetItem;
                    if (entry.Condition.Evaluate(triggerCtx) == 0) continue;
                    matchedTargetCount = pendingCount;
                    break;
                }
            }

            if (matchedTargetCount <= 0) continue;
            if ((triggerName == Trigger.UseItem || triggerName == Trigger.UseOtherItem)
                && ability.Priority == AbilityPriority.Immediate
                && executeImmediate != null)
            {
                if (matchedInvokeTargets != null && matchedInvokeTargets.Count > 0)
                {
                    for (int i = 0; i < matchedInvokeTargets.Count; i++)
                        executeImmediate(abilityId, matchedInvokeTargets[i]);
                }
                else
                {
                    executeImmediate(abilityId, invokeTargetItem);
                }
                continue;
            }

            AddOrMergeAbility(abilityId, matchedTargetCount, current, next, invokeTargetItem, matchedInvokeTargets);
        }
    }

    internal void AddOrMergeAbility(
        int abilityId,
        int pendingCount,
        AbilityQueueBuckets current,
        AbilityQueueBuckets next,
        ItemState? invokeTarget = null,
        IReadOnlyList<ItemState>? invokeTargets = null)
    {
        if (!AbilityStates.TryGetValue(abilityId, out var state))
        {
            state = new AbilityState();
            AbilityStates[abilityId] = state;
        }

        bool shouldEnqueue = state.PendingCount <= 0;
        state.PendingCount += pendingCount;
        if (invokeTargets != null && invokeTargets.Count > 0)
        {
            state.InvokeTargets ??= new Queue<ItemState>(Math.Max(4, invokeTargets.Count));
            for (int i = 0; i < invokeTargets.Count; i++)
                state.InvokeTargets.Enqueue(invokeTargets[i]);
        }
        else if (invokeTarget != null)
        {
            state.InvokeTargets ??= new Queue<ItemState>(4);
            for (int i = 0; i < pendingCount; i++)
                state.InvokeTargets.Enqueue(invokeTarget);
        }

        if (!shouldEnqueue) return;
        var ability = GetAbility(abilityId);
        int b = AbilityQueueBuckets.BucketIndex(ability.Priority);
        if (ability.Priority == AbilityPriority.Immediate) current.AddToBucket(b, abilityId);
        else next.AddToBucket(b, abilityId);
    }

}
