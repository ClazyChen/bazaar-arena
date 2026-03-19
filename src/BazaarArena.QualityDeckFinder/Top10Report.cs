namespace BazaarArena.QualityDeckFinder;

/// <summary>按 ELO 输出全局 Top10（从虚拟池）与锚定玩家每物品最强卡组（含对局数）。</summary>
public static class Top10Report
{
    private static int GetCumulativeGameCount(OptimizerState state, string comboSig)
    {
        int v = state.VirtualPlayerPool.TryGetValue(comboSig, out var ve) ? ve.GameCount : 0;
        int h = state.HistoryPool.TryGetValue(comboSig, out var he) ? he.GameCount : 0;
        return Math.Max(v, h);
    }

    public static void Print(OptimizerState state)
    {
        Console.WriteLine();
        Console.WriteLine("========== Top 10 卡组（虚拟池） ==========");
        Console.WriteLine($"赛季 {state.CurrentSeason}，总对局 {state.TotalGames}");
        var top = state.VirtualPlayerPool.Values
            .OrderByDescending(e => e.Elo)
            .Take(10)
            .ToList();
        for (int i = 0; i < top.Count; i++)
        {
            var e = top[i];
            var counts = ComboSignature.ShapeCounts(e.Representative.Shape);
            var itemsStr = string.Join(", ", e.Representative.ItemNames);
            var games = GetCumulativeGameCount(state, e.ComboSig);
            Console.WriteLine($"  {i + 1}. ELO {e.Elo:F0}  对局数 {games}  形状({counts.small},{counts.medium},{counts.large})  {itemsStr}");
        }
        Console.WriteLine("==============================================");

        PrintBestDeckByItem(state);
        Console.WriteLine();
    }

    private static void PrintBestDeckByItem(OptimizerState state)
    {
        int reportCount = Math.Max(0, state.Config.AnchoredReportCount);
        if (reportCount == 0) return;

        var byItem = new Dictionary<string, List<(string key, string comboSig)>>(StringComparer.Ordinal);
        foreach (var kv in state.AnchoredPlayerComboSig)
        {
            var itemName = AnchoredRepresentativeScheduler.ItemNameFromKey(kv.Key);
            if (string.IsNullOrEmpty(itemName)) continue;
            if (!byItem.TryGetValue(itemName, out var list))
            {
                list = new List<(string, string)>();
                byItem[itemName] = list;
            }
            list.Add((kv.Key, kv.Value));
        }

        var bestPerItem = new List<(string itemName, ComboEntry entry)>();
        foreach (var kv in byItem)
        {
            ComboEntry? best = null;
            foreach (var (_, comboSig) in kv.Value)
            {
                if (!state.VirtualPlayerPool.TryGetValue(comboSig, out var e)) continue;
                if (best == null || e.Elo > best.Elo || (e.Elo == best.Elo && e.GameCount > best.GameCount))
                    best = e;
            }
            if (best != null)
                bestPerItem.Add((kv.Key, best));
        }

        var ordered = bestPerItem
            .OrderByDescending(x => x.entry.Elo)
            .Take(reportCount)
            .ToList();

        Console.WriteLine();
        Console.WriteLine($"========== 物品最强拍档（锚定玩家 Top {ordered.Count}） ==========");
        foreach (var (itemName, entry) in ordered)
        {
            var counts = ComboSignature.ShapeCounts(entry.Representative.Shape);
            var itemsStr = string.Join(", ", entry.Representative.ItemNames);
            var games = GetCumulativeGameCount(state, entry.ComboSig);
            Console.WriteLine($"  {itemName}:  ELO {entry.Elo:F0}  对局数 {games}  形状({counts.small},{counts.medium},{counts.large})  {itemsStr}");
        }
        Console.WriteLine("==================================================");
    }
}
