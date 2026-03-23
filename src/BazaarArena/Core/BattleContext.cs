using BazaarArena.BattleSimulator;

namespace BazaarArena.Core;

public sealed class BattleContext
{
    public BattleState BattleState { get; set; } = null!;
    /// <summary>
    /// 当前上下文正在判定/施加效果的目标物品。
    /// 光环判定时表示“正在判断是否能接受光环”的物品；
    /// trigger condition 与 effect apply 中默认与 Caster 一致。
    /// </summary>
    public ItemState Item { get; set; } = null!;
    public ItemState Caster { get; set; } = null!;
    public ItemState? Source { get; set; }
    public ItemState? InvokeTarget { get; set; }
}
