namespace BazaarArena.QualityDeckFinder;

/// <summary>等级 2 下 6 槽点的所有合法形状（每部分 ∈ {1,2,3}）。</summary>
public static class Shapes
{
    /// <summary>预定义的 7 种形状：槽位从左到右的尺寸列表。</summary>
    public static IReadOnlyList<IReadOnlyList<int>> All { get; } =
    [
        [1, 1, 1, 1, 1, 1],           // 6 小
        [1, 1, 1, 1, 2],             // 4 小 + 1 中
        [1, 1, 1, 3],                // 3 小 + 1 大
        [1, 1, 2, 2],                // 2 小 + 2 中
        [1, 2, 3],                   // 1 小 + 1 中 + 1 大
        [2, 2, 2],                   // 3 中
        [3, 3],                      // 2 大
    ];

    /// <summary>形状的槽位数量（即列表长度）。</summary>
    public static int SlotCount(IReadOnlyList<int> shape) => shape.Count;

    /// <summary>形状的友好显示字符串，如 "3+2+1"。</summary>
    public static string ToDisplayString(IReadOnlyList<int> shape) =>
        string.Join("+", shape);

    /// <summary>根据形状索引获取形状（0..6）。</summary>
    public static IReadOnlyList<int> ByIndex(int index)
    {
        if (index < 0 || index >= All.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return All[index];
    }
}
