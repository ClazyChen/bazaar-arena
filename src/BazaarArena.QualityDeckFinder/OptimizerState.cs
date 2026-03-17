using System.Collections.Concurrent;

namespace BazaarArena.QualityDeckFinder;

/// <summary>优化器状态：单一池（组合+代表排列+ELO）、分段定义、计数与可选 RNG 种子。Pool 与计数为多线程安全。</summary>
public sealed class OptimizerState
{
    public Config Config { get; }
    public Priors Priors { get; } = new();

    /// <summary>入池/踢人时使用的锁，保证 TryAddToPool 整段逻辑原子。</summary>
    public object PoolSync { get; } = new object();

    /// <summary>池中所有组合：comboSignature -> ComboEntry。</summary>
    public ConcurrentDictionary<string, ComboEntry> Pool { get; } = new(StringComparer.Ordinal);

    private int _totalRestarts;
    private int _totalClimbs;
    private int _totalGames;

    public int TotalRestarts { get => Volatile.Read(ref _totalRestarts); set => Volatile.Write(ref _totalRestarts, value); }
    public int TotalClimbs { get => Volatile.Read(ref _totalClimbs); set => Volatile.Write(ref _totalClimbs, value); }
    public int TotalGames { get => Volatile.Read(ref _totalGames); set => Volatile.Write(ref _totalGames, value); }
    public int? RngSeed { get; set; }

    public void IncrementTotalRestarts() => Interlocked.Increment(ref _totalRestarts);
    public void IncrementTotalClimbs() => Interlocked.Increment(ref _totalClimbs);
    public void IncrementTotalGames() => Interlocked.Increment(ref _totalGames);

    public OptimizerState(Config config)
    {
        Config = config;
        Priors.EmaAlpha = config.PriorEmaAlpha;
    }

    public int SegmentIndex(double elo)
    {
        lock (Config.SegmentBoundsLock)
        {
            var bounds = Config.SegmentBounds;
            for (int i = 0; i < bounds.Count; i++)
                if (elo < bounds[i])
                    return i;
            return bounds.Count;
        }
    }

    /// <summary>当前池中属于某段的卡组（按 ELO 归属）。</summary>
    public List<string> SignaturesInSegment(int segmentIndex)
    {
        IReadOnlyList<double> bounds;
        lock (Config.SegmentBoundsLock)
        {
            bounds = Config.SegmentBounds.ToList();
        }
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
