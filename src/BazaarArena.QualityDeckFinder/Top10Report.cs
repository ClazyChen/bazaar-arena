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

        PrintBestDeckByItem(state);
        Console.WriteLine();
    }

    private static void PrintBestDeckByItem(OptimizerState state)
    {
        int reportCount = Math.Max(0, state.Config.AnchoredReportCount);
        if (reportCount == 0) return;
        if (state.Pool.Count == 0) return;

        // itemName -> best entry
        var best = new Dictionary<string, ComboEntry>(StringComparer.Ordinal);
        foreach (var entry in state.Pool.Values)
        {
            foreach (var item in entry.Representative.ItemNames)
            {
                if (!best.TryGetValue(item, out var ex) || entry.Elo > ex.Elo)
                    best[item] = entry;
            }
        }

        var ordered = best
            .Select(kv => (item: kv.Key, entry: kv.Value))
            .OrderByDescending(x => x.entry.Elo)
            .Take(reportCount)
            .ToList();

        Console.WriteLine();
        Console.WriteLine($"========== 物品最强拍档（Top {ordered.Count}） ==========");
        foreach (var x in ordered)
        {
            var counts = ComboSignature.ShapeCounts(x.entry.Representative.Shape);
            var itemsStr = string.Join(", ", x.entry.Representative.ItemNames);
            Console.WriteLine($"  {x.item}:  ELO {x.entry.Elo:F0}  形状计数({counts.small},{counts.medium},{counts.large})  {itemsStr}");
        }
        Console.WriteLine("===============================================");
    }
}
