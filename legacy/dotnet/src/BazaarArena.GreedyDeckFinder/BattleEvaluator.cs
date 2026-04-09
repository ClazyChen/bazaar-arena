using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using SimulatorClass = BazaarArena.BattleSimulator.BattleSimulator;
using System.Collections.Concurrent;

namespace BazaarArena.GreedyDeckFinder;

/// <summary>对战评估器：单局、BO5 与平局裁决。</summary>
public sealed class BattleEvaluator
{
    /// <summary>并行执行一批 BO/系列赛所需的最少对局数；单对局走串行以避免 Parallel.For 固定开销。</summary>
    private const int ParallelPairsMinCount = 2;
    private const int SaltPlayBoNBatch = unchecked((int)0x504C424Eu); // 'PLBN'
    private const int SaltPlaySeriesBatch = unchecked((int)0x53525353u); // 'SRSS'

    public readonly record struct MatchPoints(double A, double B);

    private readonly SimulatorClass _simulator;
    private readonly IItemTemplateResolver _db;
    private readonly Random _rng;
    private readonly int _bestOf;
    private readonly int _workers;
    private readonly int _playerLevel;
    private readonly PerfStats _perf;
    private readonly ConcurrentDictionary<string, Deck> _deckCache = new(StringComparer.Ordinal);
    /// <summary>与 <see cref="Program"/> 的 <c>--seed</c> 一致时，并行批内可为对局派生 <see cref="Random"/>；批内顺序经打乱，不保证与按原始下标绑定的随机流一致（项目约定：统计可接受即可，见 <c>.cursor/rules/greedy-deck-finder.mdc</c>）。未指定 seed 时并行仍可用线程局部随机。</summary>
    private readonly int? _deterministicBattleSeed;
    private long _parallelBattleBatchSeq;

    public BattleEvaluator(
        SimulatorClass simulator,
        IItemTemplateResolver db,
        Random rng,
        int bestOf,
        int workers,
        int playerLevel,
        PerfStats perf,
        int? deterministicBattleSeed = null)
    {
        _simulator = simulator;
        _db = db;
        _rng = rng;
        _bestOf = bestOf;
        _workers = Math.Max(0, workers);
        _playerLevel = playerLevel;
        _perf = perf;
        _deterministicBattleSeed = deterministicBattleSeed;
    }

    public int PlayBoN(DeckRep a, DeckRep b)
    {
        _perf.IncBoSeries();
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        int need = (_bestOf / 2) + 1;
        int winsA = 0;
        int winsB = 0;
        while (winsA < need && winsB < need)
        {
            int w = PlaySingleGame(a, b, dedicatedRng: null, useTlsWhenDedicatedNull: false);
            if (w == 0) winsA++;
            else winsB++;
        }
        _perf.AddBoTicks(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
        return winsA > winsB ? 0 : 1;
    }

    public int[] PlayBoNBatch(IReadOnlyList<(DeckRep a, DeckRep b)> pairs)
    {
        var winners = new int[pairs.Count];
        if (pairs.Count == 0) return winners;
        long tBatch = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_workers <= 1 || pairs.Count < ParallelPairsMinCount)
        {
            for (int i = 0; i < pairs.Count; i++)
                winners[i] = PlayBoN(pairs[i].a, pairs[i].b);
            _perf.RecordBoSerialBatchWall(System.Diagnostics.Stopwatch.GetTimestamp() - tBatch, pairs.Count);
            return winners;
        }

        var order = CreateShuffledPairOrder(pairs.Count);
        long batchId = System.Threading.Interlocked.Increment(ref _parallelBattleBatchSeq);
        Parallel.For(0, pairs.Count, new ParallelOptions { MaxDegreeOfParallelism = _workers }, slot =>
        {
            int j = order[slot];
            Random? dedicated = CreateDedicatedRngForPair(batchId, slot, SaltPlayBoNBatch);
            winners[j] = PlayBoNParallel(pairs[j].a, pairs[j].b, dedicated);
        });
        _perf.RecordBoParallelBatchWall(System.Diagnostics.Stopwatch.GetTimestamp() - tBatch, pairs.Count, _workers);
        return winners;
    }

    public MatchPoints[] PlaySeriesBatch(IReadOnlyList<(DeckRep a, DeckRep b)> pairs, int gameCount)
    {
        var results = new MatchPoints[pairs.Count];
        if (pairs.Count == 0) return results;
        long tBatch = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_workers <= 1 || pairs.Count < ParallelPairsMinCount)
        {
            for (int i = 0; i < pairs.Count; i++)
                results[i] = PlaySeriesPointsCore(pairs[i].a, pairs[i].b, gameCount, dedicatedRng: null, useTlsWhenDedicatedNull: false);
            _perf.RecordSeriesSerialBatchWall(System.Diagnostics.Stopwatch.GetTimestamp() - tBatch, pairs.Count);
            return results;
        }

        var order = CreateShuffledPairOrder(pairs.Count);
        long batchId = System.Threading.Interlocked.Increment(ref _parallelBattleBatchSeq);
        Parallel.For(0, pairs.Count, new ParallelOptions { MaxDegreeOfParallelism = _workers }, slot =>
        {
            int j = order[slot];
            Random? dedicated = CreateDedicatedRngForPair(batchId, slot, SaltPlaySeriesBatch);
            results[j] = PlaySeriesPointsCore(pairs[j].a, pairs[j].b, gameCount, dedicated, dedicated == null);
        });
        _perf.RecordSeriesParallelBatchWall(System.Diagnostics.Stopwatch.GetTimestamp() - tBatch, pairs.Count, _workers);
        return results;
    }

    public MatchPoints PlaySeriesPoints(DeckRep a, DeckRep b, int gameCount, bool useThreadLocalRandom = false)
    {
        return PlaySeriesPointsCore(a, b, gameCount, dedicatedRng: null, useTlsWhenDedicatedNull: useThreadLocalRandom);
    }

    private MatchPoints PlaySeriesPointsCore(DeckRep a, DeckRep b, int gameCount, Random? dedicatedRng, bool useTlsWhenDedicatedNull)
    {
        _perf.IncBoSeries();
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        int rounds = Math.Max(1, gameCount);
        double pointsA = 0;
        double pointsB = 0;
        for (int i = 0; i < rounds; i++)
        {
            var r = PlaySingleGameResult(a, b, dedicatedRng, useTlsWhenDedicatedNull, randomJudgeOnAbsoluteDraw: false);
            if (r == 0) pointsA += 1;
            else if (r == 1) pointsB += 1;
            else
            {
                pointsA += 0.5;
                pointsB += 0.5;
            }
        }
        _perf.AddBoTicks(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
        return new MatchPoints(pointsA, pointsB);
    }

    /// <summary>生成 0..count-1 的随机排列，用于并行批内打散对局顺序以缓解 BO/系列赛任务时长相关的负载不均。</summary>
    private int[] CreateShuffledPairOrder(int count)
    {
        var order = new int[count];
        for (int k = 0; k < count; k++)
            order[k] = k;
        lock (_rng)
        {
            for (int i = count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
        }
        return order;
    }

    private Random? CreateDedicatedRngForPair(long batchId, int pairIndex, int salt)
    {
        if (!_deterministicBattleSeed.HasValue)
            return null;
        return new Random(MixSeed(_deterministicBattleSeed.Value, batchId, pairIndex, salt));
    }

    private static int MixSeed(int baseSeed, long batchId, int pairIndex, int salt)
    {
        var h = new HashCode();
        h.Add(baseSeed);
        h.Add(batchId);
        h.Add(pairIndex);
        h.Add(salt);
        return h.ToHashCode();
    }

    private int PlayBoNParallel(DeckRep a, DeckRep b, Random? dedicatedRng)
    {
        _perf.IncBoSeries();
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        int need = (_bestOf / 2) + 1;
        int winsA = 0;
        int winsB = 0;
        bool useTls = dedicatedRng == null;
        while (winsA < need && winsB < need)
        {
            int w = PlaySingleGame(a, b, dedicatedRng, useTls);
            if (w == 0) winsA++;
            else winsB++;
        }
        _perf.AddBoTicks(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
        return winsA > winsB ? 0 : 1;
    }

    private int PlaySingleGame(DeckRep a, DeckRep b, Random? dedicatedRng, bool useTlsWhenDedicatedNull)
    {
        return PlaySingleGameResult(a, b, dedicatedRng, useTlsWhenDedicatedNull, randomJudgeOnAbsoluteDraw: true) switch
        {
            0 => 0,
            1 => 1,
            _ => Next2(dedicatedRng, useTlsWhenDedicatedNull),
        };
    }

    /// <summary>
    /// 返回 0 表示 A 胜，1 表示 B 胜，-1 表示绝对平局（平局且最终生命值相等）。
    /// </summary>
    private int PlaySingleGameResult(
        DeckRep a,
        DeckRep b,
        Random? dedicatedRng,
        bool useTlsWhenDedicatedNull,
        bool randomJudgeOnAbsoluteDraw)
    {
        _perf.IncSingleGame();
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var deckA = ToDeck(a);
        var deckB = ToDeck(b);
        int swap = Next2(dedicatedRng, useTlsWhenDedicatedNull);
        Deck d0 = swap == 0 ? deckA : deckB;
        Deck d1 = swap == 0 ? deckB : deckA;

        var sink = new HpTrackingSink();
        int winner = _simulator.Run(d0, d1, _db, sink, BattleLogLevel.None);
        if (winner >= 0)
        {
            _perf.AddSingleGameTicks(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
            return swap == 0 ? winner : 1 - winner;
        }

        int hpA = swap == 0 ? sink.LastHp0 : sink.LastHp1;
        int hpB = swap == 0 ? sink.LastHp1 : sink.LastHp0;
        int r;
        if (hpA > hpB) r = 0;
        else if (hpB > hpA) r = 1;
        else r = randomJudgeOnAbsoluteDraw ? Next2(dedicatedRng, useTlsWhenDedicatedNull) : -1;
        _perf.AddSingleGameTicks(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
        return r;
    }

    private int Next2(Random? dedicatedRng, bool useTlsWhenDedicatedNull)
    {
        if (dedicatedRng != null)
            return dedicatedRng.Next(2);
        if (useTlsWhenDedicatedNull)
            return ThreadLocalRandom.Next(2);
        lock (_rng)
            return _rng.Next(2);
    }

    private Deck ToDeck(DeckRep rep)
    {
        var sig = rep.Signature();
        var tier = GreedyLevelRules.CombatTier(_playerLevel);
        return _deckCache.GetOrAdd(sig, _ =>
        {
            var slots = rep.ItemNames.Select(name => new DeckSlotEntry
            {
                ItemName = name,
                Tier = tier,
                Overrides = null,
            }).ToList();
            return new Deck
            {
                PlayerLevel = _playerLevel,
                Slots = slots,
            };
        });
    }

    private sealed class HpTrackingSink : IBattleLogSink
    {
        public int LastHp0 { get; private set; }
        public int LastHp1 { get; private set; }
        public void OnFrameStart(int timeMs, int frame) { }
        public void OnHpSnapshot(int timeMs, int side0Hp, int side1Hp) { LastHp0 = side0Hp; LastHp1 = side1Hp; }
        public void OnCast(ItemState caster, string itemName, int timeMs, int? ammoRemainingAfter = null) { }
        public void OnEffect(ItemState caster, string itemName, string effectKind, int value, int timeMs, bool isCrit = false, string? extraSuffix = null) { }
        public void OnBurnTick(BattleSide victim, int burnDamage, int remainingBurn, int timeMs) { }
        public void OnPoisonTick(BattleSide victim, int poisonDamage, int timeMs) { }
        public void OnRegenTick(BattleSide side, int heal, int timeMs) { }
        public void OnSandstormTick(int damage, int timeMs) { }
        public void OnResult(int winnerSideIndex, int timeMs, bool isDraw) { }
    }
}
