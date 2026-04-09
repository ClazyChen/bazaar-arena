using BazaarArena.Core;
using System.Linq;

namespace BazaarArena.BattleSimulator;

/// <summary>单场战斗内复用的触发器/光环/能力状态索引。</summary>
public sealed class BattleSessionTables
{
    public List<int>[] AbilitiesByTrigger { get; } =
        Enumerable.Range(0, Trigger.Count).Select(_ => new List<int>()).ToArray();

    public Dictionary<int, List<int>> AurasByAttribute { get; } = [];
}

