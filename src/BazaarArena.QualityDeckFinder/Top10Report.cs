namespace BazaarArena.QualityDeckFinder;

/// <summary>按 ELO 排序输出 Top10。</summary>
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
            var counts = ComboSignature.ShapeCounts(e.Representative.Shape);
            var itemsStr = string.Join(", ", e.Representative.ItemNames);
            var confirm = e.IsConfirmed ? "已确认" : "未确认";
            Console.WriteLine($"  {i + 1}. ELO {e.Elo:F0}  形状计数({counts.small},{counts.medium},{counts.large})  {confirm}  {itemsStr}");
        }
        Console.WriteLine("==================================");
        Console.WriteLine();
    }
}
