namespace BazaarArena.Core;

/// <summary>己方物品某 key 的聚合方式：取最高值或最低值。</summary>
public enum SideSelectKind
{
    Max,
    Min,
}

/// <summary>公式求值上下文接口：供 Formula 按名取来源/己方/敌方字段及按条件计数，与 BattleSimulator 解耦。</summary>
public interface IFormulaContext
{
    /// <summary>当前光环来源物品的模板字段 key 的值（含光环）。</summary>
    int GetSourceInt(string key);
    /// <summary>己方阵营字典 key 的值。</summary>
    int GetSideInt(string key);
    /// <summary>敌方阵营字典 key 的值；无敌方时返回 0。</summary>
    int GetOppInt(string key);
    /// <summary>双方未摧毁物品中满足 condition 的数量（Source=光环来源，Item=候选物品）。</summary>
    int Count(Condition? condition);
    /// <summary>己方未摧毁物品中 key 字段的最高值；无物品时返回 0。</summary>
    int GetSideItemMax(string key);
    /// <summary>己方未摧毁物品中 key 字段的最低值；无物品时返回 0。</summary>
    int GetSideItemMin(string key);
}

/// <summary>光环固定加成公式：委托类型，支持 Source/Side/Opp/Count/Constant 与加减乘组合，由 Formula.Evaluate 统一求值。</summary>
public class Formula
{
    private readonly Func<IFormulaContext, int> _evaluate;

    private Formula(Func<IFormulaContext, int> evaluate) => _evaluate = evaluate;

    /// <summary>求值。</summary>
    public int Evaluate(IFormulaContext ctx) => _evaluate(ctx);

    /// <summary>当前物品字段 key 的值（如 Key.Damage、Key.Custom_0）。</summary>
    public static Formula Source(string key) => new(ctx => ctx.GetSourceInt(key));
    /// <summary>己方阵营字段（如 BattleSide.KeyPoison）。</summary>
    public static Formula Side(string key) => new(ctx => ctx.GetSideInt(key));
    /// <summary>敌方阵营字段。</summary>
    public static Formula Opp(string key) => new(ctx => ctx.GetOppInt(key));
    /// <summary>双方未摧毁且满足条件的物品数。</summary>
    public static Formula Count(Condition? condition) => new(ctx => ctx.Count(condition));
    /// <summary>常数。</summary>
    public static Formula Constant(int value) => new(_ => value);
    /// <summary>己方物品中 key 字段的聚合值（Max=最高，Min=最低）；无物品时返回 0。</summary>
    public static Formula SideSelect(string key, SideSelectKind kind) =>
        new(ctx => kind == SideSelectKind.Max ? ctx.GetSideItemMax(key) : ctx.GetSideItemMin(key));

    /// <summary>对公式结果做变换，用于如 RatioUtil.PercentFloor(formula, percent)。</summary>
    public static Formula Apply(Formula f, Func<int, int> transform) => new(ctx => transform(f.Evaluate(ctx)));

    /// <summary>对两个公式求值后合并，用于如 RatioUtil.PercentFloor(valueFormula, percentFormula)（percent 来自字段或公式）。</summary>
    public static Formula Apply(Formula a, Formula b, Func<int, int, int> combine) => new(ctx => combine(a.Evaluate(ctx), b.Evaluate(ctx)));

    public static Formula operator +(Formula a, Formula b) => new(ctx => a.Evaluate(ctx) + b.Evaluate(ctx));
    public static Formula operator -(Formula a, Formula b) => new(ctx => a.Evaluate(ctx) - b.Evaluate(ctx));
    public static Formula operator -(Formula f) => new(ctx => -f.Evaluate(ctx));
    public static Formula operator *(Formula a, Formula b) => new(ctx => a.Evaluate(ctx) * b.Evaluate(ctx));
    public static Formula operator *(int n, Formula f) => new(ctx => n * f.Evaluate(ctx));
    public static Formula operator *(Formula f, int n) => new(ctx => f.Evaluate(ctx) * n);
}
