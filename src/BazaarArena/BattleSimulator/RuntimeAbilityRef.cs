using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>战斗内能力引用：绑定能力定义与持有者物品。</summary>
public sealed class RuntimeAbilityRef
{
    private static int s_nextId = 1;
    public RuntimeAbilityRef(ItemState owner, AbilityDefinition ability)
    {
        Id = System.Threading.Interlocked.Increment(ref s_nextId);
        Owner = owner;
        Ability = ability;
    }

    public int Id { get; }
    public ItemState Owner { get; }
    public AbilityDefinition Ability { get; }
}

