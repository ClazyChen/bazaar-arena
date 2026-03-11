using BazaarArena.BattleSimulator;

namespace BazaarArena.Core;

/// <summary>条件评估上下文：己方/敌方阵营与被评估物品、可选参考物品（同一方/相邻等由 Item/Source 的 SideIndex/ItemIndex 推导）。</summary>
public readonly struct ConditionContext
{
    /// <summary>被评估物品所在侧。</summary>
    public BattleSide MySide { get; init; }
    /// <summary>对方侧。</summary>
    public BattleSide EnemySide { get; init; }
    /// <summary>当前被评估的物品（即「这一个物品」是否满足条件）。</summary>
    public BattleItemState Item { get; init; }
    /// <summary>可选参考物品，用于 SameAsSource、AdjacentToSource、RightOfSource、InFlight 等。</summary>
    public BattleItemState? Source { get; init; }
}

/// <summary>通用条件：由委托表示谓词，用于光环或能力触发。支持 And 组合（如「己方其他物品」= DifferentFromSource && SameSide）。</summary>
public class Condition
{
    private readonly Func<ConditionContext, bool>? _evaluate;

    /// <summary>使用给定委托创建条件（用于克隆等）。</summary>
    public Condition(Func<ConditionContext, bool>? evaluate) => _evaluate = evaluate;

    /// <summary>评估条件；null 委托视为恒真。</summary>
    public bool Evaluate(ConditionContext ctx) => _evaluate?.Invoke(ctx) ?? true;

    /// <summary>克隆（复制委托引用），供模板克隆时使用。</summary>
    public static Condition? Clone(Condition? c) => c == null ? null : new Condition(c._evaluate);

    /// <summary>目标与来源相同（光环：自身；触发器：仅来源物品触发）。</summary>
    public static Condition SameAsSource { get; } = new(ctx =>
        ctx.Source != null && ctx.Item.SideIndex == ctx.Source.SideIndex && ctx.Item.ItemIndex == ctx.Source.ItemIndex);

    /// <summary>目标与来源不同（触发器：除来源外的物品；不限定己方/敌方）。</summary>
    public static Condition DifferentFromSource { get; } = new(ctx =>
        ctx.Source == null || ctx.Item.SideIndex != ctx.Source.SideIndex || ctx.Item.ItemIndex != ctx.Source.ItemIndex);

    /// <summary>候选与来源同侧（己方）。</summary>
    public static Condition SameSide { get; } = new(ctx =>
        ctx.Source != null && ctx.Item.SideIndex == ctx.Source.SideIndex);

    /// <summary>候选与来源异侧（敌方）。</summary>
    public static Condition DifferentSide { get; } = new(ctx =>
        ctx.Source != null && ctx.Item.SideIndex != ctx.Source.SideIndex);

    /// <summary>目标与来源相邻（同侧且 |Item.ItemIndex - Source.ItemIndex| == 1）。</summary>
    public static Condition AdjacentToSource { get; } = new(ctx =>
        ctx.Source != null && ctx.Item.SideIndex == ctx.Source.SideIndex && Math.Abs(ctx.Item.ItemIndex - ctx.Source.ItemIndex) == 1);

    /// <summary>候选在来源同侧且紧贴来源右侧（Item.ItemIndex == Source.ItemIndex + 1）。用于目标选择如「施放者右侧物品」。</summary>
    public static Condition RightOfSource { get; } = new(ctx =>
        ctx.Source != null && ctx.Item.SideIndex == ctx.Source.SideIndex && ctx.Item.ItemIndex == ctx.Source.ItemIndex + 1);

    /// <summary>有参条件：被评估物品或参考物品带指定标签。由调用方将「被使用物品」等设为 Item 或 Source。</summary>
    public static Condition WithTag(string tag) => new(ctx =>
        (ctx.Item.Template.Tags?.Contains(tag) ?? false) || (ctx.Source?.Template.Tags?.Contains(tag) ?? false));

    /// <summary>被评估物品为护盾物品（依据模板 Tag.Shield，如 ReduceAttribute 遍历敌方时用 Item 判断）。</summary>
    public static Condition IsShieldItem { get; } = new(ctx =>
        ctx.Item.Template.Tags?.Contains(Tag.Shield) ?? false);

    /// <summary>使用物品时：被使用的物品在能力持有者（Item）的右侧，即同侧且 Source.ItemIndex == Item.ItemIndex + 1（Source=被使用物品、Item=能力持有者）。</summary>
    public static Condition UsedItemRightOfSource { get; } = new(ctx =>
        ctx.Source != null && ctx.Item.SideIndex == ctx.Source.SideIndex && ctx.Source.ItemIndex == ctx.Item.ItemIndex + 1);

    /// <summary>两个条件的与，用于组合语义（如「己方其他物品」= And(DifferentFromSource, SameSide)）。</summary>
    public static Condition And(Condition a, Condition b) => new(ctx => a.Evaluate(ctx) && b.Evaluate(ctx));

    /// <summary>光环时：提供光环的物品处于飞行状态（Source.InFlight）。</summary>
    public static Condition InFlight { get; } = new(ctx => ctx.Source != null && ctx.Source.InFlight);

    /// <summary>被评估物品为大型或处于飞行状态；用于 Destroy 的 InvokeTargetCondition（被摧毁目标是否大型或飞行）。</summary>
    public static Condition LargeOrInFlight { get; } = new(ctx =>
        ctx.Item.Template.Size == ItemSize.Large || ctx.Item.InFlight);
}
