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

    /// <summary>锚定玩家当前卡组：key = "itemName|shapeIndex"，value = comboSig。</summary>
    public ConcurrentDictionary<string, string> AnchoredPlayerComboSig { get; } = new(StringComparer.Ordinal);

    /// <summary>强度玩家当前卡组列表（每项为一个 comboSig）。仅主线程/赛季步骤内修改。</summary>
    public List<string> StrengthPlayerComboSigs { get; } = new();

    /// <summary>每个强度玩家上次改进的赛季（与 StrengthPlayerComboSigs 一一对应）；用于放弃判定。</summary>
    public List<int> StrengthLastImprovedSeason { get; } = new();

    /// <summary>每个锚定玩家（key）上次改进的赛季；用于放弃判定。</summary>
    public Dictionary<string, int> AnchoredLastImprovedSeason { get; } = new(StringComparer.Ordinal);

    /// <summary>当前赛季编号（用于报告与断点续跑）。</summary>
    public int CurrentSeason { get; set; }

    /// <summary>组合最近对局统计（可选，用于后续分析）。</summary>
    public ConcurrentDictionary<string, ComboStats> StatsByComboSig { get; } = new(StringComparer.Ordinal);

    /// <summary>锚定玩家 key：itemName + "|" + shapeIndex。</summary>
    public static string AnchoredKey(string itemName, int shapeIndex) => $"{itemName}|{shapeIndex}";

    private int _totalRestarts;
    private int _totalClimbs;
    private int _totalGames;
    private int _anchorPickCounter;

    public int TotalRestarts { get => Volatile.Read(ref _totalRestarts); set => Volatile.Write(ref _totalRestarts, value); }
    public int TotalClimbs { get => Volatile.Read(ref _totalClimbs); set => Volatile.Write(ref _totalClimbs, value); }
    public int TotalGames { get => Volatile.Read(ref _totalGames); set => Volatile.Write(ref _totalGames, value); }
    public int? RngSeed { get; set; }

    public void IncrementTotalRestarts() => Interlocked.Increment(ref _totalRestarts);
    public void IncrementTotalClimbs() => Interlocked.Increment(ref _totalClimbs);
    public void IncrementTotalGames() => Interlocked.Increment(ref _totalGames);

    public int NextAnchorPickIndex() => Interlocked.Increment(ref _anchorPickCounter);

    public OptimizerState(Config config)
    {
        Config = config;
        Priors.EmaAlpha = config.PriorEmaAlpha;
    }

    public sealed class ComboStats
    {
        /// <summary>仅保留最近若干条对局记录（ring buffer 简化实现）。</summary>
        public List<MatchRecord> Recent { get; } = new();

        /// <summary>对 Recent 的访问锁（多 worker 模式下 RecordMatch 会并发调用）。</summary>
        public object Sync { get; } = new object();
    }

    public readonly record struct MatchRecord(
        int SelfSegmentAtTime,
        int OpponentSegmentAtTime,
        sbyte Outcome // 1=胜, 0=平, -1=负
    );

    /// <summary>记录一次对局结果（仅记录 comboSig 的视角）。</summary>
    public void RecordMatch(string comboSig, double selfEloAtTime, double opponentEloAtTime, int winner)
    {
        // winner: 0 表示 self 赢；1 表示对手赢；-1 平局（见 EloSystem 的调用约定）
        sbyte outcome = winner < 0 ? (sbyte)0 : (winner == 0 ? (sbyte)1 : (sbyte)-1);
        int selfSeg = SegmentIndex(selfEloAtTime);
        int oppSeg = SegmentIndex(opponentEloAtTime);

        var stats = StatsByComboSig.GetOrAdd(comboSig, _ => new ComboStats());
        lock (stats.Sync)
        {
            stats.Recent.Add(new MatchRecord(selfSeg, oppSeg, outcome));
            const int cap = 50;
            if (stats.Recent.Count > cap)
                stats.Recent.RemoveRange(0, stats.Recent.Count - cap);
        }
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
        // segmentIndex 可能大于 bounds.Count（最高段），此时 lo 应取最后一个边界。
        var lo = segmentIndex == 0 ? 0.0 : (segmentIndex - 1 >= bounds.Count ? bounds[^1] : bounds[segmentIndex - 1]);
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
