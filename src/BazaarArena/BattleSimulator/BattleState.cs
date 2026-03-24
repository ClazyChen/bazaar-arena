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

    internal AbilityQueueBuckets CurrentAbilityBuckets { get; set; } = new();
    internal AbilityQueueBuckets NextAbilityBuckets { get; set; } = new();
    private int? _invokeOnlyForSideIndex;
    private Action<int>? _invokeExecuteImmediate;
    private AbilityQueueBuckets? _invokeCurrentBuckets;
    private AbilityQueueBuckets? _invokeNextBuckets;

    internal IDisposable BeginInvokeScope(
        int? onlyForSideIndex = null,
        Action<int>? executeImmediate = null,
        AbilityQueueBuckets? current = null,
        AbilityQueueBuckets? next = null)
    {
        var scope = new InvokeScope(
            this,
            _invokeOnlyForSideIndex,
            _invokeExecuteImmediate,
            _invokeCurrentBuckets,
            _invokeNextBuckets);
        _invokeOnlyForSideIndex = onlyForSideIndex;
        _invokeExecuteImmediate = executeImmediate;
        _invokeCurrentBuckets = current;
        _invokeNextBuckets = next;
        return scope;
    }

    public void InvokeTrigger(int triggerName, ItemState? causeItem, ItemState? invokeTargetItem, int? invokeCount = null) =>
        InvokeTrigger(triggerName, causeItem, invokeTargetItem, null, invokeCount);

    public void InvokeTriggerMany(int triggerName, ItemState? causeItem, IReadOnlyList<ItemState> invokeTargetItems, int? invokeCount = null) =>
        InvokeTrigger(triggerName, causeItem, null, invokeTargetItems, invokeCount);

    internal void InvokeTrigger(
        int triggerName,
        ItemState? causeItem,
        ItemState? invokeTargetItem,
        IReadOnlyList<ItemState>? invokeTargetItems,
        int? invokeCount)
    {
        var current = _invokeCurrentBuckets ?? CurrentAbilityBuckets;
        var next = _invokeNextBuckets ?? NextAbilityBuckets;
        var executeImmediate = _invokeExecuteImmediate;
        var onlyForSideIndex = _invokeOnlyForSideIndex;
        int pendingCount = (triggerName == Trigger.UseItem || triggerName == Trigger.Freeze || triggerName == Trigger.Slow || triggerName == Trigger.Haste || triggerName == Trigger.Burn || triggerName == Trigger.Poison) && invokeCount is int m ? m : 1;
        var triggerCtx = new BattleContext
        {
            BattleState = this,
        };
        var indices = new List<int>(32);

        void VisitOwnerSide(int ownerSideIndex, BattleSide ownerSide)
        {
            indices.Clear();
            if (causeItem != null && !causeItem.Destroyed && ownerSideIndex == causeItem.SideIndex && causeItem.ItemIndex < ownerSide.Items.Count)
            {
                indices.Add(causeItem.ItemIndex);
                for (int i = 0; i < ownerSide.Items.Count; i++)
                {
                    if (i != causeItem.ItemIndex)
                        indices.Add(i);
                }
            }
            else
            {
                for (int i = 0; i < ownerSide.Items.Count; i++)
                    indices.Add(i);
            }

            if (SessionTables == null)
                return;
            var triggerAbilities = SessionTables.AbilitiesByTrigger[triggerName];

            foreach (int ownerItemIndex in indices)
            {
                var abilityOwner = ownerSide.Items[ownerItemIndex];
                if (abilityOwner.Destroyed) continue;
                foreach (int abilityId in triggerAbilities)
                {
                    var owner = GetAbilityOwner(abilityId);
                    if (!ReferenceEquals(owner, abilityOwner))
                        continue;
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
                                triggerCtx.Item = abilityOwner;
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
                            triggerCtx.Item = abilityOwner;
                            triggerCtx.Caster = abilityOwner;
                            triggerCtx.Source = causeItem ?? abilityOwner;
                            triggerCtx.InvokeTarget = invokeTargetItem;
                            if (entry.Condition.Evaluate(triggerCtx) == 0) continue;
                            matchedTargetCount = pendingCount;
                            break;
                        }
                    }

                    if (matchedTargetCount <= 0) continue;
                    if (ability.Priority == AbilityPriority.Immediate && executeImmediate != null)
                    {
                        executeImmediate(abilityId);
                        continue;
                    }

                    AddOrMergeAbility(abilityId, matchedTargetCount, current, next, invokeTargetItem, matchedInvokeTargets);
                }
            }
        }

        if (onlyForSideIndex is int ofs)
        {
            VisitOwnerSide(ofs, Side[ofs]);
        }
        else if (causeItem != null && !causeItem.Destroyed)
        {
            VisitOwnerSide(causeItem.SideIndex, Side[causeItem.SideIndex]);
            VisitOwnerSide(1 - causeItem.SideIndex, Side[1 - causeItem.SideIndex]);
        }
        else
        {
            VisitOwnerSide(0, Side[0]);
            VisitOwnerSide(1, Side[1]);
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
            state.InvokeTargets ??= new List<ItemState>(Math.Max(4, invokeTargets.Count));
            for (int i = 0; i < invokeTargets.Count; i++)
                state.InvokeTargets.Add(invokeTargets[i]);
        }
        else if (invokeTarget != null)
        {
            state.InvokeTargets ??= new List<ItemState>(4);
            for (int i = 0; i < pendingCount; i++)
                state.InvokeTargets.Add(invokeTarget);
        }

        if (!shouldEnqueue) return;
        var ability = GetAbility(abilityId);
        int b = AbilityQueueBuckets.BucketIndex(ability.Priority);
        if (ability.Priority == AbilityPriority.Immediate) current.AddToBucket(b, abilityId);
        else next.AddToBucket(b, abilityId);
    }

    private sealed class InvokeScope : IDisposable
    {
        private readonly BattleState _state;
        private readonly int? _previousOnlyForSideIndex;
        private readonly Action<int>? _previousExecuteImmediate;
        private readonly AbilityQueueBuckets? _previousCurrentBuckets;
        private readonly AbilityQueueBuckets? _previousNextBuckets;
        private bool _disposed;

        public InvokeScope(
            BattleState state,
            int? previousOnlyForSideIndex,
            Action<int>? previousExecuteImmediate,
            AbilityQueueBuckets? previousCurrentBuckets,
            AbilityQueueBuckets? previousNextBuckets)
        {
            _state = state;
            _previousOnlyForSideIndex = previousOnlyForSideIndex;
            _previousExecuteImmediate = previousExecuteImmediate;
            _previousCurrentBuckets = previousCurrentBuckets;
            _previousNextBuckets = previousNextBuckets;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _state._invokeOnlyForSideIndex = _previousOnlyForSideIndex;
            _state._invokeExecuteImmediate = _previousExecuteImmediate;
            _state._invokeCurrentBuckets = _previousCurrentBuckets;
            _state._invokeNextBuckets = _previousNextBuckets;
        }
    }
}
