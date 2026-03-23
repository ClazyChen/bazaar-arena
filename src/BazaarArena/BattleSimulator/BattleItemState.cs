using BazaarArena.Core;

namespace BazaarArena.BattleSimulator;

/// <summary>
/// 兼容过渡类型：已由 ItemState（字段直存）取代。
/// 后续整体重构完成后可删除此适配层。
/// </summary>
public class BattleItemState : ItemState
{
    public BattleItemState(ItemTemplate template, ItemTier tier) : base(template, tier) { }
}
