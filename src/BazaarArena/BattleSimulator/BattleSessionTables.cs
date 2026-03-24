using BazaarArena.Core;
using System.Linq;

namespace BazaarArena.BattleSimulator;

/// <summary>单场战斗内复用的触发器/光环/能力状态索引。</summary>
public sealed class BattleSessionTables
{
    public List<RuntimeAbilityRef>[] AbilitiesByTrigger { get; } =
        Enumerable.Range(0, Trigger.Count).Select(_ => new List<RuntimeAbilityRef>()).ToArray();

    public Dictionary<int, Dictionary<ItemState, List<RuntimeAbilityRef>>> AbilitiesByTriggerAndOwner { get; } = [];

    public Dictionary<int, List<(ItemState Source, AuraDefinition Aura)>> AurasByAttribute { get; } = [];

    public List<(ItemState Source, AuraDefinition Aura)> AllAuras { get; } = [];

    public Dictionary<int, AbilityState> AbilityRefIdToState { get; } = [];
}

