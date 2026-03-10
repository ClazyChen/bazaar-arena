namespace BazaarArena.Core;

/// <summary>条件评估上下文：候选与来源的阵营/物品下标，以及可选的「被使用物品」/「候选物品」模板（供 WithTag 等使用）。</summary>
public readonly struct ConditionContext
{
    public int CandidateSide { get; init; }
    public int CandidateItem { get; init; }
    public int SourceSide { get; init; }
    public int SourceItem { get; init; }
    /// <summary>触发时「被使用的物品」模板（如 UseOtherItem）；光环评估时为 null。</summary>
    public ItemTemplate? UsedTemplate { get; init; }
    /// <summary>候选/目标物品模板（光环评估时用；触发时也可填）。用于 WithTag 等。</summary>
    public ItemTemplate? CandidateTemplate { get; init; }
    /// <summary>光环评估时：提供光环的物品是否处于飞行状态。用于 Condition.InFlight。</summary>
    public bool SourceInFlight { get; init; }
    /// <summary>候选物品的类型快照（如 ReduceAttribute 遍历敌方时填入）。用于 IsShieldItem 等条件。</summary>
    public ItemTypeSnapshot? CandidateTypeSnapshot { get; init; }
    /// <summary>OnDestroy 触发时：被摧毁物品的模板（用于如「被毁目标为大型」判定）。</summary>
    public ItemTemplate? DestroyedItemTemplate { get; init; }
    /// <summary>OnDestroy 触发时：被摧毁物品是否处于飞行状态。</summary>
    public bool DestroyedItemInFlight { get; init; }
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
        ctx.CandidateSide == ctx.SourceSide && ctx.CandidateItem == ctx.SourceItem);

    /// <summary>目标与来源不同（触发器：除来源外的物品；不限定己方/敌方）。</summary>
    public static Condition DifferentFromSource { get; } = new(ctx =>
        ctx.CandidateSide != ctx.SourceSide || ctx.CandidateItem != ctx.SourceItem);

    /// <summary>候选与来源同侧（己方）。</summary>
    public static Condition SameSide { get; } = new(ctx =>
        ctx.CandidateSide == ctx.SourceSide);

    /// <summary>候选与来源异侧（敌方）。</summary>
    public static Condition DifferentSide { get; } = new(ctx =>
        ctx.CandidateSide != ctx.SourceSide);

    /// <summary>目标与来源相邻（同侧且 |sourceIndex - targetIndex| == 1）。</summary>
    public static Condition AdjacentToSource { get; } = new(ctx =>
        ctx.CandidateSide == ctx.SourceSide && Math.Abs(ctx.CandidateItem - ctx.SourceItem) == 1);

    /// <summary>候选在来源同侧且紧贴来源右侧（CandidateItem == SourceItem + 1）。用于目标选择如「施放者右侧物品」。</summary>
    public static Condition RightOfSource { get; } = new(ctx =>
        ctx.CandidateSide == ctx.SourceSide && ctx.CandidateItem == ctx.SourceItem + 1);

    /// <summary>有参条件：被使用物品或候选物品带指定标签。触发时看 UsedTemplate，光环时看 CandidateTemplate。</summary>
    public static Condition WithTag(string tag) => new(ctx =>
        (ctx.UsedTemplate?.Tags?.Contains(tag) ?? false) || (ctx.CandidateTemplate?.Tags?.Contains(tag) ?? false));

    /// <summary>候选物品为护盾物品（需在上下文中填入 CandidateTypeSnapshot，如 ReduceAttribute 遍历敌方时）。</summary>
    public static Condition IsShieldItem { get; } = new(ctx =>
        ctx.CandidateTypeSnapshot?.IsShieldItem ?? false);

    /// <summary>UseOtherItem 时：被使用的物品在来源（能力持有者）的右侧，即同侧且 UsedItemIndex == SourceItemIndex + 1。InvokeTrigger 中 Source=被使用物品、Candidate=能力持有者，故为 SameSide 且 SourceItem == CandidateItem + 1。</summary>
    public static Condition UsedItemRightOfSource { get; } = new(ctx =>
        ctx.CandidateSide == ctx.SourceSide && ctx.SourceItem == ctx.CandidateItem + 1);

    /// <summary>两个条件的与，用于组合语义（如「己方其他物品」= And(DifferentFromSource, SameSide)）。</summary>
    public static Condition And(Condition a, Condition b) => new(ctx => a.Evaluate(ctx) && b.Evaluate(ctx));

    /// <summary>光环时：提供光环的物品处于飞行状态（SourceInFlight）。</summary>
    public static Condition InFlight { get; } = new(ctx => ctx.SourceInFlight);

    /// <summary>OnDestroy 触发时：被摧毁目标为大型物品或处于飞行状态。</summary>
    public static Condition DestroyedTargetIsLargeOrInFlight { get; } = new(ctx =>
        ctx.DestroyedItemTemplate?.Size == ItemSize.Large || ctx.DestroyedItemInFlight);
}
