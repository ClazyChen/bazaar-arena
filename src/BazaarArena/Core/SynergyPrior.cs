namespace BazaarArena.Core;

/// <summary>效果目标相对己方物品的方向；仅对下游有意义。</summary>
public enum SynergyDirection
{
    Any,
    Left,
    Right,
}

/// <summary>协同先验子句：同一子句内为 AND 关系；多个子句之间为 OR。Direction 仅对下游有意义。</summary>
public sealed class SynergyClause
{
    /// <summary>效果目标方向；上游/邻居时忽略（视为 Any）。</summary>
    public SynergyDirection Direction { get; set; }

    /// <summary>本子句内需要满足的 Tag（AND）。</summary>
    public List<string> Tags { get; set; } = [];

    public static SynergyClause And(params string[] tags) => new() { Tags = [..tags] };
    public static SynergyClause And(SynergyDirection direction, params string[] tags) =>
        new() { Direction = direction, Tags = [..tags] };

    /// <summary>将单个 Tag 视为一个 AND 子句（便于列表字面量书写）。</summary>
    public static implicit operator SynergyClause(string tag) => And(tag);

    /// <summary>将 Tag 转为子句并指定方向（仅对下游有意义）。</summary>
    public static SynergyClause ToClause(string tag, SynergyDirection direction) => And(direction, tag);

    /// <summary>将当前子句设置方向并返回新子句（不修改原对象）。</summary>
    public SynergyClause WithDirection(SynergyDirection direction) => new() { Direction = direction, Tags = [..Tags] };

    // 不提供 & 运算符：C# 无法为 string 重载运算符，若要 & 需要引入包装类型，会降低可读性。
}
