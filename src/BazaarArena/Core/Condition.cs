using BazaarArena.BattleSimulator;

namespace BazaarArena.Core;

/// <summary>条件评估上下文：己方/敌方阵营、能力持有者（Source）与当前被评估对象（Item）。Condition 时 Item=引起触发的物品，InvokeTargetCondition 时 Item=触发器指向的物品，TargetCondition 时 Item=候选目标；Source 恒为能力所属物品且非空，Item 可能为空（如 BattleStart 无触发者）。评估 TargetCondition 时可选填入 InvokeTargetItem，供 SameAsInvokeTarget 限定「仅该物品」。</summary>
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
    /// <summary>触发器指向的单个目标（如被加速/被减速的物品）；仅在评估 TargetCondition 且能力由带 InvokeTarget 的触发入队时非空，供 SameAsInvokeTarget 限定「仅该物品」。</summary>
    public BattleItemState? InvokeTargetItem { get; init; }

    /// <summary>按「被评估对象」解析其有效标签（含光环授予，如「相邻视为载具」）。为 null 时 WithTag/NotWithTag 仅用 Item.Template.Tags；非空时用此委托返回的集合。计算光环授予时须传入仅用 Template.Tags 的委托以避免循环。</summary>
    public Func<BattleItemState, IReadOnlySet<string>>? GetEffectiveTagsForItem { get; init; }
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

    /// <summary>总是满足的条件。</summary>
    public static Condition Always { get; } = new(_ => true);

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

    /// <summary>被评估对象与触发器指向的目标相同（Item == InvokeTargetItem）；用于 TargetCondition / additionalTargetCondition，使效果仅施加给「被加速/被减速」等的那一件物品。InvokeTargetItem 或 Item 为 null 时为 false。</summary>
    public static Condition SameAsInvokeTarget { get; } = new(ctx =>
        ctx.Item != null && ctx.InvokeTargetItem != null
        && ctx.Item.SideIndex == ctx.InvokeTargetItem.SideIndex
        && ctx.Item.ItemIndex == ctx.InvokeTargetItem.ItemIndex);

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

    /// <summary>被评估对象在能力持有者严格左侧（同侧且 Item.ItemIndex &lt; Source.ItemIndex）；用于「此物品左侧每有一件…」计数。Item 为 null 时为 false。</summary>
    public static Condition StrictlyLeftOfSource { get; } = new(ctx =>
        ctx.Item != null && ctx.Item.SideIndex == ctx.Source.SideIndex && ctx.Item.ItemIndex < ctx.Source.ItemIndex);

    /// <summary>被评估对象在能力持有者严格右侧（同侧且 Item.ItemIndex &gt; Source.ItemIndex）；用于「此物品右侧每有一件…」计数。Item 为 null 时为 false。</summary>
    public static Condition StrictlyRightOfSource { get; } = new(ctx =>
        ctx.Item != null && ctx.Item.SideIndex == ctx.Source.SideIndex && ctx.Item.ItemIndex > ctx.Source.ItemIndex);

    /// <summary>被评估对象带指定标签（含光环授予的有效标签）；Item 为 null 时为 false。能力持有者带某 tag 时在 AbilityDefinition 上用 SourceCondition = WithTag(tag)，评估时 Item=Source。</summary>
    public static Condition WithTag(string tag) => new(ctx =>
    {
        if (ctx.Item == null) return false;
        var tags = ctx.GetEffectiveTagsForItem?.Invoke(ctx.Item) ?? new HashSet<string>(ctx.Item.Template.Tags ?? []);
        return tags.Contains(tag);
    });

    /// <summary>被评估对象不带指定标签（Item 为 null 或有效标签不包含 tag 时为 true）。用于「非武器」「非工具」等目标筛选。</summary>
    public static Condition NotWithTag(string tag) => new(ctx =>
        ctx.Item == null || !(ctx.GetEffectiveTagsForItem?.Invoke(ctx.Item) ?? new HashSet<string>(ctx.Item.Template.Tags ?? [])).Contains(tag));

    /// <summary>被评估对象具备可暴击的六类数值之一（护盾/伤害/灼烧/剧毒/治疗/再生）；用于 AddAttribute/ReduceAttribute 暴击率时的隐含目标条件。</summary>
    public static Condition HasAnyCrittableTag { get; } = new(ctx =>
    {
        if (ctx.Item == null) return false;
        var tags = ctx.GetEffectiveTagsForItem?.Invoke(ctx.Item) ?? new HashSet<string>(ctx.Item.Template.Tags ?? []);
        return tags.Contains(Tag.Damage) || tags.Contains(Tag.Burn) || tags.Contains(Tag.Poison)
            || tags.Contains(Tag.Heal) || tags.Contains(Tag.Shield) || tags.Contains(Tag.Regen);
    });

    /// <summary>被评估对象可参与暴击判定：具备可暴击六类 Tag 且至少有一条 UseItem+UseSelf+ApplyCritMultiplier 能力。用于 add/reduce 暴击率时仅对可暴击物品生效。</summary>
    public static Condition CanCrit { get; } = new(ctx =>
    {
        if (ctx.Item == null || !HasAnyCrittableTag.Evaluate(ctx)) return false;
        var abilities = ctx.Item.Template.Abilities;
        if (abilities == null) return false;
        foreach (var a in abilities)
        {
            if (a.TriggerName == Trigger.UseItem && a.UseSelf && a.ApplyCritMultiplier)
                return true;
        }
        return false;
    });

    /// <summary>被评估对象模板带指定标签（仅读 Template.Tags，不含光环授予）；Item 为 null 时为 false。用于避免光环递归（如弹药物品用 Tag.Ammo）。</summary>
    public static Condition WithTemplateTag(string tag) => new(ctx =>
        ctx.Item != null && ctx.Item.Template.Tags != null && ctx.Item.Template.Tags.Contains(tag));

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

    /// <summary>被评估对象（Item）未处于飞行状态；Item 为 null 时为 false。用于 StartFlying 的 additionalTargetCondition。</summary>
    public static Condition NotInFlight { get; } = new(ctx => ctx.Item != null && !ctx.Item.InFlight);

    /// <summary>被评估对象未摧毁；Item 为 null 时为 false。用于目标选取（充能/冻结/减速/加速等）。</summary>
    public static Condition NotDestroyed { get; } = new(ctx => ctx.Item != null && !ctx.Item.Destroyed);

    /// <summary>被评估对象已摧毁；Item 为 null 时为 false。用于修复目标选取。</summary>
    public static Condition Destroyed { get; } = new(ctx => ctx.Item != null && ctx.Item.Destroyed);

    /// <summary>被评估对象有冷却时间（CooldownMs &gt; 0）；Item 为 null 时为 false。用于目标选取（充能/冻结/减速/加速等）。</summary>
    public static Condition HasCooldown { get; } = new(ctx => ctx.Item != null && ctx.MySide.GetItemInt(ctx.Item.ItemIndex, "CooldownMs", 0) > 0);

    /// <summary>被评估对象弹药已耗尽（模板带 Tag.Ammo 且 AmmoRemaining = 0）；Item 为 null 时为 false。用于 Trigger.Ammo 的 additionalCondition；用 WithTemplateTag 避免光环递归。弹药物品由注册时 AmmoCap &gt; 0 自动获得 Tag.Ammo。</summary>
    public static Condition AmmoDepleted { get; } = WithTemplateTag(Tag.Ammo) & new Condition(ctx => ctx.Item != null && ctx.Item.AmmoRemaining == 0);

    /// <summary>被评估对象处于冻结状态（FreezeRemainingMs &gt; 0）；Item 为 null 时为 false。用于解除冻结目标选取。</summary>
    public static Condition IsFrozen { get; } = new(ctx => ctx.Item != null && ctx.Item.FreezeRemainingMs > 0);

    /// <summary>被评估对象是能力持有者最左侧的物品（Item.ItemIndex == 0）；Item 为 null 时为 false。用于「敌人使用其最左侧的物品时」等。</summary>
    public static Condition Leftmost { get; } = new(ctx => ctx.Item != null && ctx.Item.ItemIndex == 0);

    /// <summary>被评估对象（Item）是己方唯一伙伴：同侧未摧毁且带 Tag.Friend 的物品恰有一个且为该 Item。用于光环 SourceCondition（如「若此为唯一伙伴则暴击率加成」）。计数时使用有效标签（含光环授予）。</summary>
    public static Condition OnlyCompanion { get; } = new(ctx =>
    {
        if (ctx.Item == null || ctx.Item.Destroyed) return false;
        var itemTags = ctx.GetEffectiveTagsForItem?.Invoke(ctx.Item) ?? new HashSet<string>(ctx.Item.Template.Tags ?? []);
        if (!itemTags.Contains(Tag.Friend)) return false;
        int count = 0;
        for (int j = 0; j < ctx.MySide.Items.Count; j++)
        {
            var it = ctx.MySide.Items[j];
            if (it.Destroyed) continue;
            var t = ctx.GetEffectiveTagsForItem?.Invoke(it) ?? new HashSet<string>(it.Template.Tags ?? []);
            if (t.Contains(Tag.Friend)) count++;
        }
        return count == 1;
    });

    /// <summary>被评估对象（Item）是己方唯一有冷却时间的武器：同侧未摧毁、带 Tag.Weapon 且 CooldownMs &gt; 0 的物品恰有一个且为该 Item。用于「若此为唯一有冷却的武器则装填弹药」等。计数时使用有效标签（含光环授予）。</summary>
    public static Condition OnlyWeaponWithCooldown { get; } = new(ctx =>
    {
        if (ctx.Item == null || ctx.Item.Destroyed) return false;
        var itemTags = ctx.GetEffectiveTagsForItem?.Invoke(ctx.Item) ?? new HashSet<string>(ctx.Item.Template.Tags ?? []);
        if (!itemTags.Contains(Tag.Weapon)) return false;
        if (ctx.MySide.GetItemInt(ctx.Item.ItemIndex, "CooldownMs", 0) <= 0) return false;
        int count = 0;
        for (int j = 0; j < ctx.MySide.Items.Count; j++)
        {
            var it = ctx.MySide.Items[j];
            if (it.Destroyed) continue;
            var t = ctx.GetEffectiveTagsForItem?.Invoke(it) ?? new HashSet<string>(it.Template.Tags ?? []);
            if (!t.Contains(Tag.Weapon)) continue;
            if (ctx.MySide.GetItemInt(it.ItemIndex, "CooldownMs", 0) <= 0) continue;
            count++;
        }
        return count == 1;
    });

    /// <summary>能力持有者（Source）的 Custom_0 为 0；用于「首次使用前」暴击率加成等（如靴里剑首次使用 +100% 暴击率）。</summary>
    public static Condition SourceCustom0IsZero { get; } = new(ctx =>
        ctx.MySide.GetItemInt(ctx.Source.ItemIndex, Key.Custom_0, 0) == 0);

    /// <summary>被评估对象是己方满足 inner 条件的物品中 ItemIndex 最小的那一件（左端）。用于「己方最左侧的武器」等。遍历时跳过已摧毁。</summary>
    public static Condition LeftMost(Condition inner) => new(ctx =>
    {
        if (ctx.Item == null || ctx.Item.SideIndex != ctx.Source.SideIndex) return false;
        if (ctx.Item.Destroyed) return false;
        if (!inner.Evaluate(ctx)) return false;
        int minIdx = int.MaxValue;
        for (int i = 0; i < ctx.MySide.Items.Count; i++)
        {
            var it = ctx.MySide.Items[i];
            if (it.Destroyed) continue;
            var subCtx = ctx with { Item = it };
            if (inner.Evaluate(subCtx))
                minIdx = Math.Min(minIdx, i);
        }
        return minIdx != int.MaxValue && ctx.Item.ItemIndex == minIdx;
    });

    /// <summary>被评估对象是己方满足 inner 条件的物品中 ItemIndex 最大的那一件（右端）。用于「己方最右侧的武器」等。遍历时跳过已摧毁。</summary>
    public static Condition RightMost(Condition inner) => new(ctx =>
    {
        if (ctx.Item == null || ctx.Item.SideIndex != ctx.Source.SideIndex) return false;
        if (ctx.Item.Destroyed) return false;
        if (!inner.Evaluate(ctx)) return false;
        int maxIdx = -1;
        for (int i = 0; i < ctx.MySide.Items.Count; i++)
        {
            var it = ctx.MySide.Items[i];
            if (it.Destroyed) continue;
            var subCtx = ctx with { Item = it };
            if (inner.Evaluate(subCtx))
                maxIdx = Math.Max(maxIdx, i);
        }
        return maxIdx >= 0 && ctx.Item.ItemIndex == maxIdx;
    });
}
