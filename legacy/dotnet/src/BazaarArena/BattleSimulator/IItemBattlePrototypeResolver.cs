using BazaarArena.ItemDatabase;

namespace BazaarArena.BattleSimulator;

/// <summary>
/// 可选：在搜索/批跑前按名称构造好的铜档战斗原型（通常为扁平化模板 + <see cref="ItemState(ItemTemplate, ItemTier)"/> 一次的结果）。
/// 实现方须保证返回的 <see cref="ItemState"/> 在进程内不被写入（仅作为 <see cref="ItemState.ItemState(ItemState)"/> 的拷贝源）。
/// </summary>
public interface IItemBattlePrototypeResolver : IItemTemplateResolver
{
    /// <summary>若存在该名称的战斗原型则返回其引用，否则 null（调用方应使用模板构造）。</summary>
    ItemState? TryGetBattlePrototype(string name);
}
