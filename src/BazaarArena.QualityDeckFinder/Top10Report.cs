namespace BazaarArena.QualityDeckFinder;

/// <summary>按 ELO 排序输出 Top10 与局部最优标记。</summary>
public static class Top10Report
{
    public static void Print(OptimizerState state)
    {
        var top = state.Pool.Values
            .OrderByDescending(e => e.Elo)
            .Take(10)
            .ToList();

        Console.WriteLine();
        Console.WriteLine("========== Top 10 卡组 ==========");
        Console.WriteLine($"总重启 {state.TotalRestarts}，总爬山 {state.TotalClimbs}，总对局 {state.TotalGames}");
        for (int i = 0; i < top.Count; i++)
        {
            var e = top[i];
            var shapeStr = Shapes.ToDisplayString(e.Deck.Shape);
            var itemsStr = string.Join(", ", e.Deck.ItemNames.Take(5));
            if (e.Deck.ItemNames.Count > 5) itemsStr += "...";
            var opt = e.IsLocalOptimum ? " [局部最优]" : "";
            Console.WriteLine($"  {i + 1}. ELO {e.Elo:F0}  {shapeStr}  {itemsStr}{opt}");
        }
        Console.WriteLine("==================================");
        Console.WriteLine();
    }
}
