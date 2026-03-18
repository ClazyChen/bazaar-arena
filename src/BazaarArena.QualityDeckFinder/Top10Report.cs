namespace BazaarArena.QualityDeckFinder;

/// <summary>按 ELO 输出强度玩家 Top10 与锚定玩家每物品最强卡组（含对局数）。</summary>
public static class Top10Report
{
    public static void Print(OptimizerState state)
    {
        var strengthSig = state.StrengthPlayerComboSigs.Distinct(StringComparer.Ordinal).ToList();
        var top = strengthSig
            .Select(sig => state.Pool.TryGetValue(sig, out var e) ? (sig, e) : (sig, (ComboEntry?)null))
            .Where(x => x.Item2 != null)
            .OrderByDescending(x => x.Item2!.Elo)
            .Take(10)
            .ToList();

        Console.WriteLine();
        Console.WriteLine("========== Top 10 卡组（强度玩家） ==========");
        Console.WriteLine($"赛季 {state.CurrentSeason}，总对局 {state.TotalGames}");
        for (int i = 0; i < top.Count; i++)
        {
            var e = top[i].Item2!;
            var counts = ComboSignature.ShapeCounts(e.Representative.Shape);
            var itemsStr = string.Join(", ", e.Representative.ItemNames);
            Console.WriteLine($"  {i + 1}. ELO {e.Elo:F0}  对局数 {e.GameCount}  形状({counts.small},{counts.medium},{counts.large})  {itemsStr}");
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
                if (!state.Pool.TryGetValue(comboSig, out var e)) continue;
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
            Console.WriteLine($"  {itemName}:  ELO {entry.Elo:F0}  对局数 {entry.GameCount}  形状({counts.small},{counts.medium},{counts.large})  {itemsStr}");
        }
        Console.WriteLine("==================================================");
    }
}
