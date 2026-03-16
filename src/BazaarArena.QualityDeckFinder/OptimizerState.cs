namespace BazaarArena.QualityDeckFinder;

/// <summary>优化器状态：单一池（卡组+ELO+是否局部最优）、分段定义、计数与可选 RNG 种子。</summary>
public sealed class OptimizerState
{
    public Config Config { get; }

    /// <summary>池中所有卡组：signature -> (DeckRep, ELO, IsLocalOptimum, GameCount)。</summary>
    public Dictionary<string, DeckEntry> Pool { get; } = new(StringComparer.Ordinal);

    public int TotalRestarts { get; set; }
    public int TotalClimbs { get; set; }
    public int TotalGames { get; set; }
    public int? RngSeed { get; set; }

    public OptimizerState(Config config)
    {
        Config = config;
    }

    public int SegmentIndex(double elo)
    {
        var bounds = Config.SegmentBounds;
        for (int i = 0; i < bounds.Count; i++)
            if (elo < bounds[i])
                return i;
        return bounds.Count;
    }

    /// <summary>当前池中属于某段的卡组（按 ELO 归属）。</summary>
    public List<string> SignaturesInSegment(int segmentIndex)
    {
        var bounds = Config.SegmentBounds;
        var lo = segmentIndex == 0 ? 0.0 : bounds[segmentIndex - 1];
        var hi = segmentIndex >= bounds.Count ? double.MaxValue : bounds[segmentIndex];
        var list = new List<string>();
        foreach (var kv in Pool)
        {
            var elo = kv.Value.Elo;
            if (elo >= lo && elo < hi)
                list.Add(kv.Key);
        }
        return list;
    }
}

public sealed record DeckEntry(DeckRep Deck, double Elo, bool IsLocalOptimum, int GameCount);
