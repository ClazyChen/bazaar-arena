namespace BazaarArena.Core;

/// <summary>统一公式：输入 BattleContext，输出 int。条件语义中 0=false, 非0=true。</summary>
public class Formula
{
    private readonly Func<BattleContext, int> _evaluate;

    public Formula(Func<BattleContext, int> evaluate) => _evaluate = evaluate;

    /// <summary>求值。</summary>
    public int Evaluate(BattleContext ctx) => _evaluate(ctx);

    /// <summary>读取「能力持有者」模板整型；与上下文字段 <see cref="BattleContext.Source"/>（引起触发者）区分，此处固定走 <see cref="BattleContext.Caster"/>。</summary>
    public static Formula Source(int key) => new(ctx =>
    {
        if (ctx.Caster == null) return 0;
        return ctx.Caster.Template.GetInt(key, ctx.Caster.Tier, 0);
    });

    public static Formula Caster(int key) => new(ctx =>
    {
        if (ctx.Caster == null) return 0;
        return ctx.Caster.Template.GetInt(key, ctx.Caster.Tier, 0);
    });

    public static Formula Item(int key) => new(ctx =>
    {
        return ctx.Item.Template.GetInt(key, ctx.Item.Tier, 0);
    });

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
        if (ctx.BattleState == null) return 0;
        int n = 0;
        var side0 = ctx.BattleState.Side0;
        var side1 = ctx.BattleState.Side1;
        if (side0 != null)
        {
            foreach (var it in side0.Items)
            {
                var c = new BattleContext
                {
                    BattleState = ctx.BattleState,
                    Item = it,
                    Caster = ctx.Caster,
                    Source = ctx.Source,
                    InvokeTarget = ctx.InvokeTarget,
                };
                if (condition.Evaluate(c) != 0) n++;
            }
        }
        if (side1 != null)
        {
            foreach (var it in side1.Items)
            {
                var c = new BattleContext
                {
                    BattleState = ctx.BattleState,
                    Item = it,
                    Caster = ctx.Caster,
                    Source = ctx.Source,
                    InvokeTarget = ctx.InvokeTarget,
                };
                if (condition.Evaluate(c) != 0) n++;
            }
        }
        return n;
    });

    private static BazaarArena.BattleSimulator.BattleSide CurrentSide(BattleContext ctx)
    {
        int sideIndex = ctx.Caster?.SideIndex ?? ctx.Source?.SideIndex ?? ctx.Item.SideIndex;
        return sideIndex == 0 ? ctx.BattleState.Side0 : ctx.BattleState.Side1;
    }

    private static BazaarArena.BattleSimulator.BattleSide OppSide(BattleContext ctx)
    {
        int sideIndex = ctx.Caster?.SideIndex ?? ctx.Source?.SideIndex ?? ctx.Item.SideIndex;
        return sideIndex == 0 ? ctx.BattleState.Side1 : ctx.BattleState.Side0;
    }

    private static int ReadSideAttribute(BazaarArena.BattleSimulator.BattleSide side, int key)
    {
        if (key == Key.SideIndex) return side.SideIndex;
        if (key == Key.Damage) return side.MaxHp;
        if (key == Key.Heal) return side.Hp;
        if (key == Key.Shield) return side.Shield;
        if (key == Key.Burn) return side.Burn;
        if (key == Key.Poison) return side.Poison;
        if (key == Key.Regen) return side.Regen;
        if (key == Key.Gold) return side.Gold;
        return 0;
    }

    public static Formula Side(string sideKey) => new(ctx =>
    {
        if (!Key.TryGetKey(sideKey, out int k)) return 0;
        return ReadSideAttribute(CurrentSide(ctx), k);
    });

    public static Formula Opp(string sideKey) => new(ctx =>
    {
        if (!Key.TryGetKey(sideKey, out int k)) return 0;
        return ReadSideAttribute(OppSide(ctx), k);
    });
    public static Formula Side(int key) => new(ctx => ReadSideAttribute(CurrentSide(ctx), key));
    public static Formula Opp(int key) => new(ctx => ReadSideAttribute(OppSide(ctx), key));
    public static Formula SideSelect(int key, SideSelectKind kind) => new(ctx =>
    {
        int a = ReadSideAttribute(ctx.BattleState.Side0, key);
        int b = ReadSideAttribute(ctx.BattleState.Side1, key);
        return kind == SideSelectKind.Min ? Math.Min(a, b) : Math.Max(a, b);
    });

    public static Formula operator +(Formula a, Formula b) => new(ctx => a.Evaluate(ctx) + b.Evaluate(ctx));
    public static Formula operator -(Formula a, Formula b) => new(ctx => a.Evaluate(ctx) - b.Evaluate(ctx));
    public static Formula operator -(Formula f) => new(ctx => -f.Evaluate(ctx));
    public static Formula operator *(Formula a, Formula b) => new(ctx => a.Evaluate(ctx) * b.Evaluate(ctx));
    public static Formula operator *(int n, Formula f) => new(ctx => n * f.Evaluate(ctx));
    public static Formula operator *(Formula f, int n) => new(ctx => f.Evaluate(ctx) * n);
    public static Formula operator &(Formula a, Formula b) => new(ctx => (a.Evaluate(ctx) != 0 && b.Evaluate(ctx) != 0) ? 1 : 0);
    public static Formula operator |(Formula a, Formula b) => new(ctx => (a.Evaluate(ctx) != 0 || b.Evaluate(ctx) != 0) ? 1 : 0);
}
