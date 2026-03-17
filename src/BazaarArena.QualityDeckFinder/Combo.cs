using System.Text;

namespace BazaarArena.QualityDeckFinder;

/// <summary>
/// 组合签名：与排列无关。由形状计数 (小/中/大槽位数) + 物品集合（排序）构成。
/// </summary>
public static class ComboSignature
{
    public static (int small, int medium, int large) ShapeCounts(IReadOnlyList<int> shape)
    {
        int s = 0, m = 0, l = 0;
        foreach (var x in shape)
        {
            if (x == 1) s++;
            else if (x == 2) m++;
            else if (x == 3) l++;
        }
        return (s, m, l);
    }

    public static string FromCountsAndItems((int small, int medium, int large) counts, IReadOnlyList<string> itemNames)
    {
        var sorted = itemNames.OrderBy(x => x, StringComparer.Ordinal).ToList();
        var sb = new StringBuilder();
        sb.Append("c=").Append(counts.small).Append(",").Append(counts.medium).Append(",").Append(counts.large);
        sb.Append("|");
        sb.Append(string.Join(",", sorted));
        return sb.ToString();
    }

    public static string FromDeckRep(DeckRep rep)
    {
        var counts = ShapeCounts(rep.Shape);
        return FromCountsAndItems(counts, rep.ItemNames);
    }
}

/// <summary>组合条目：以“代表排列 DeckRep”作为对战用的具体排列。</summary>
public sealed record ComboEntry(
    string ComboSig,
    DeckRep Representative,
    double Elo,
    bool IsLocalOptimum,
    bool IsConfirmed,
    int GameCount);

