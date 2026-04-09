namespace BazaarArena.Core;

/// <summary>统一公式：输入 BattleContext，输出 int。条件语义中 0=false, 非0=true。</summary>
public class Formula
{
    private readonly Func<BattleContext, int> _evaluate;

    public Formula(Func<BattleContext, int> evaluate) => _evaluate = evaluate;

    /// <summary>求值。</summary>
    public int Evaluate(BattleContext ctx) => _evaluate(ctx);

    public static Formula Caster(int key) => new(ctx => ctx.GetItemInt(ctx.Caster, key));

    public static Formula Item(int key) => new(ctx => ctx.GetItemInt(ctx.Item, key));

    /// <summary>常数。</summary>
    public static Formula Constant(int value) => new(_ => value);
    public static Formula True { get; } = Constant(1);
    public static Formula False { get; } = Constant(0);

    /// <summary>对公式结果做变换，用于如 RatioUtil.PercentFloor(formula, percent)。</summary>
    public static Formula Apply(Formula f, Func<int, int> transform) => new(ctx => transform(f.Evaluate(ctx)));

    /// <summary>对两个公式求值后合并，用于如 RatioUtil.PercentFloor(valueFormula, percentFormula)（percent 来自字段或公式）。</summary>
    public static Formula Apply(Formula a, Formula b, Func<int, int, int> combine) => new(ctx => combine(a.Evaluate(ctx), b.Evaluate(ctx)));

    public static Formula Count(Formula condition) => new(ctx =>
    {
        int n = 0;
        var side0 = ctx.BattleState.Side[0];
        var side1 = ctx.BattleState.Side[1];
        var countCtx = new BattleContext
        {
            BattleState = ctx.BattleState,
            Caster = ctx.Caster,
            Source = ctx.Source,
            InvokeTarget = ctx.InvokeTarget,
        };
        foreach (var it in side0.Items)
        {
            if (it.Destroyed) continue;
            countCtx.Item = it;
            if (condition.Evaluate(countCtx) != 0) n++;
        }
        foreach (var it in side1.Items)
        {
            if (it.Destroyed) continue;
            countCtx.Item = it;
            if (condition.Evaluate(countCtx) != 0) n++;
        }
        return n;
    });

    public static Formula Side(int key) => new(ctx => ctx.CurrentSide.GetAttribute(key));
    public static Formula Opp(int key) => new(ctx => ctx.OppSide.GetAttribute(key));
    public static Formula SideSelect(int key, SideSelectKind kind) => new(ctx =>
    {
        bool hasAny = false;
        int selected = 0;
        foreach (var item in ctx.CurrentSide.Items)
        {
            if (item.Destroyed) continue;
            int value = ctx.BattleState.GetItemInt(item, key);
            if (!hasAny)
            {
                selected = value;
                hasAny = true;
                continue;
            }
            selected = kind == SideSelectKind.Min ? Math.Min(selected, value) : Math.Max(selected, value);
        }
        return hasAny ? selected : 0;
    });

    public static Formula operator +(Formula a, Formula b) => new(ctx => a.Evaluate(ctx) + b.Evaluate(ctx));
    public static Formula operator -(Formula a, Formula b) => new(ctx => a.Evaluate(ctx) - b.Evaluate(ctx));
    public static Formula operator -(Formula f) => new(ctx => -f.Evaluate(ctx));
    public static Formula operator ~(Formula f) => new(ctx => f.Evaluate(ctx) == 0 ? 1 : 0);
    public static Formula operator *(Formula a, Formula b) => new(ctx => a.Evaluate(ctx) * b.Evaluate(ctx));
    public static Formula operator *(int n, Formula f) => new(ctx => n * f.Evaluate(ctx));
    public static Formula operator *(Formula f, int n) => new(ctx => f.Evaluate(ctx) * n);
    public static Formula operator &(Formula a, Formula b) => new(ctx => (a.Evaluate(ctx) != 0 && b.Evaluate(ctx) != 0) ? 1 : 0);
    public static Formula operator |(Formula a, Formula b) => new(ctx => (a.Evaluate(ctx) != 0 || b.Evaluate(ctx) != 0) ? 1 : 0);
}
