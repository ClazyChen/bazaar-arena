namespace BazaarArena.Core;

/// <summary>协同先验书写辅助：用于构造 AND 子句（OR 由列表表达）。</summary>
public static class Synergy
{
    /// <summary>构造一个 AND 子句（Direction=Any）。</summary>
    public static SynergyClause And(params string[] tags) => SynergyClause.And(tags);

    /// <summary>构造一个带方向的 AND 子句。</summary>
    public static SynergyClause And(SynergyDirection direction, params string[] tags) => SynergyClause.And(direction, tags);
}

