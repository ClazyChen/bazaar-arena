using BazaarArena.BattleSimulator;

namespace BazaarArena.Core;

/// <summary>条件评估上下文：己方/敌方阵营、能力持有者（Source）与当前被评估对象（Item）。Condition 时 Item=引起触发的物品，InvokeTargetCondition 时 Item=触发器指向的物品，TargetCondition 时 Item=候选目标；Source 恒为能力所属物品且非空，Item 可能为空（如 BattleStart 无触发者）。</summary>
public readonly struct ConditionContext
{
    /// <summary>能力持有者所在侧（用于推导己方/敌方）。</summary>
    public BattleSide MySide { get; init; }
    /// <summary>对方侧。</summary>
    public BattleSide EnemySide { get; init; }
    /// <summary>当前被评估对象：Condition 时为引起触发的物品，InvokeTargetCondition 时为触发器指向的物品，TargetCondition 时为候选目标；可为 null（如 BattleStart）。</summary>
    public BattleItemState? Item { get; init; }
    /// <summary>能力所属物品（参考物品），恒非空。</summary>
    public BattleItemState Source { get; init; }
}

/// <summary>通用条件：由委托表示谓词，用于光环或能力触发。支持 &amp; / | 组合（如「己方其他物品」= DifferentFromSource &amp; SameSide）。</summary>
public class Condition
{
    private readonly Func<ConditionContext, bool>? _evaluate;

    /// <summary>使用给定委托创建条件（用于克隆等）。</summary>
    public Condition(Func<ConditionContext, bool>? evaluate) => _evaluate = evaluate;

    /// <summary>评估条件；null 委托视为恒真。</summary>
    public bool Evaluate(ConditionContext ctx) => _evaluate?.Invoke(ctx) ?? true;

    /// <summary>克隆（复制委托引用），供模板克隆时使用。</summary>
    public static Condition? Clone(Condition? c) => c == null ? null : new Condition(c._evaluate);

    /// <summary>被评估对象与能力持有者相同（Item == Source）；Item 为 null 时为 false。</summary>
    public static Condition SameAsSource { get; } = new(ctx =>
        ctx.Item != null && ctx.Item.SideIndex == ctx.Source.SideIndex && ctx.Item.ItemIndex == ctx.Source.ItemIndex);

    /// <summary>被评估对象与能力持有者不同，或 Item 为 null。</summary>
    public static Condition DifferentFromSource { get; } = new(ctx =>
        ctx.Item == null || ctx.Item.SideIndex != ctx.Source.SideIndex || ctx.Item.ItemIndex != ctx.Source.ItemIndex);

    /// <summary>被评估对象与能力持有者同侧；Item 为 null 时视为同侧（如 BattleStart 默认通过）。</summary>
    public static Condition SameSide { get; } = new(ctx =>
        ctx.Item == null || ctx.Item.SideIndex == ctx.Source.SideIndex);

    /// <summary>被评估对象与能力持有者异侧；Item 为 null 时为 false。</summary>
    public static Condition DifferentSide { get; } = new(ctx =>
        ctx.Item != null && ctx.Item.SideIndex != ctx.Source.SideIndex);

    /// <summary>被评估对象与能力持有者相邻（同侧且 |Item.ItemIndex - Source.ItemIndex| == 1）；Item 为 null 时为 false。</summary>
    public static Condition AdjacentToSource { get; } = new(ctx =>
        ctx.Item != null && ctx.Item.SideIndex == ctx.Source.SideIndex && Math.Abs(ctx.Item.ItemIndex - ctx.Source.ItemIndex) == 1);

    /// <summary>被评估对象在能力持有者右侧（Item.ItemIndex == Source.ItemIndex + 1）；用于目标选择或「被使用物品在能力持有者右侧」。</summary>
    public static Condition RightOfSource { get; } = new(ctx =>
        ctx.Item != null && ctx.Item.SideIndex == ctx.Source.SideIndex && ctx.Item.ItemIndex == ctx.Source.ItemIndex + 1);

    /// <summary>被评估对象是能力持有者右侧第一个未摧毁的物品（同侧、ItemIndex &gt; Source.ItemIndex，且中间无未摧毁）；用于「摧毁右侧下一件」等。Item 为 null 时为 false。</summary>
    public static Condition FirstNonDestroyedRightOfSource { get; } = new(ctx =>
    {
        if (ctx.Item == null || ctx.Item.SideIndex != ctx.Source.SideIndex || ctx.Item.Destroyed) return false;
        if (ctx.Item.ItemIndex <= ctx.Source.ItemIndex) return false;
        for (int i = ctx.Source.ItemIndex + 1; i < ctx.Item.ItemIndex; i++)
        {
            if (!ctx.MySide.Items[i].Destroyed) return false;
        }
        return true;
    });

    /// <summary>被评估对象在能力持有者左侧（Item.ItemIndex == Source.ItemIndex - 1）；Item 为 null 时为 false。</summary>
    public static Condition LeftOfSource { get; } = new(ctx =>
        ctx.Item != null && ctx.Item.SideIndex == ctx.Source.SideIndex && ctx.Item.ItemIndex == ctx.Source.ItemIndex - 1);

    /// <summary>被评估对象带指定标签（仅看 Item）；Item 为 null 时为 false。能力持有者带某 tag 时在 AbilityDefinition 上用 SourceCondition = WithTag(tag)，评估时 Item=Source。</summary>
    public static Condition WithTag(string tag) => new(ctx =>
        ctx.Item != null && (ctx.Item.Template.Tags?.Contains(tag) ?? false));

    /// <summary>两个条件的与（也可用 a &amp;&amp; b）。</summary>
    public static Condition And(Condition a, Condition b) => new(ctx => a.Evaluate(ctx) && b.Evaluate(ctx));

    /// <summary>两个条件的或（也可用 a || b）。</summary>
    public static Condition Or(Condition a, Condition b) => new(ctx => a.Evaluate(ctx) || b.Evaluate(ctx));

    /// <summary>重载 &amp;，用于组合条件（如「己方其他物品」= DifferentFromSource &amp; SameSide）。</summary>
    public static Condition operator &(Condition a, Condition b) => And(a, b);

    /// <summary>重载 |，用于组合条件（如「大型或飞行」= Condition.WithTag(Tag.Large) | Condition.InFlight）。</summary>
    public static Condition operator |(Condition a, Condition b) => Or(a, b);

    /// <summary>被评估对象（Item）处于飞行状态；Item 为 null 时为 false。</summary>
    public static Condition InFlight { get; } = new(ctx => ctx.Item != null && ctx.Item.InFlight);

    /// <summary>被评估对象未摧毁；Item 为 null 时为 false。用于目标选取（充能/冻结/减速/加速等）。</summary>
    public static Condition NotDestroyed { get; } = new(ctx => ctx.Item != null && !ctx.Item.Destroyed);

    /// <summary>被评估对象已摧毁；Item 为 null 时为 false。用于修复目标选取。</summary>
    public static Condition Destroyed { get; } = new(ctx => ctx.Item != null && ctx.Item.Destroyed);

    /// <summary>被评估对象有冷却时间（CooldownMs &gt; 0）；Item 为 null 时为 false。用于目标选取（充能/冻结/减速/加速等）。</summary>
    public static Condition HasCooldown { get; } = new(ctx => ctx.Item != null && ctx.MySide.GetItemInt(ctx.Item.ItemIndex, "CooldownMs", 0) > 0);

    /// <summary>被评估对象（Item）是己方唯一伙伴：同侧未摧毁且带 Tag.Friend 的物品恰有一个且为该 Item。用于光环 SourceCondition（如「若此为唯一伙伴则暴击率加成」）。</summary>
    public static Condition OnlyCompanion { get; } = new(ctx =>
    {
        if (ctx.Item == null || ctx.Item.Destroyed) return false;
        if (ctx.Item.Template.Tags?.Contains(Tag.Friend) != true) return false;
        int count = 0;
        for (int j = 0; j < ctx.MySide.Items.Count; j++)
        {
            var it = ctx.MySide.Items[j];
            if (!it.Destroyed && it.Template.Tags?.Contains(Tag.Friend) == true) count++;
        }
        return count == 1;
    });
}
