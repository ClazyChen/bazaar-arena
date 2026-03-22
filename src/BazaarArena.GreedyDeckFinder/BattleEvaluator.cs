using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using SimulatorClass = BazaarArena.BattleSimulator.BattleSimulator;
using System.Collections.Concurrent;

namespace BazaarArena.GreedyDeckFinder;

/// <summary>对战评估器：单局、BO5 与平局裁决。</summary>
public sealed class BattleEvaluator
{
    private const int ParallelPairsThreshold = 16;
    public readonly record struct MatchPoints(double A, double B);

    private readonly SimulatorClass _simulator;
    private readonly IItemTemplateResolver _db;
    private readonly Random _rng;
    private readonly int _bestOf;
    private readonly int _workers;
    private readonly int _playerLevel;
    private readonly PerfStats _perf;
    private readonly ConcurrentDictionary<string, Deck> _deckCache = new(StringComparer.Ordinal);

    public BattleEvaluator(SimulatorClass simulator, IItemTemplateResolver db, Random rng, int bestOf, int workers, int playerLevel, PerfStats perf)
    {
        _simulator = simulator;
        _db = db;
        _rng = rng;
        _bestOf = bestOf;
        _workers = Math.Max(0, workers);
        _playerLevel = playerLevel;
        _perf = perf;
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
            int w = PlaySingleGame(a, b, useThreadLocalRandom: false);
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
        if (_workers <= 1 || pairs.Count < ParallelPairsThreshold)
        {
            for (int i = 0; i < pairs.Count; i++)
                winners[i] = PlayBoN(pairs[i].a, pairs[i].b);
            return winners;
        }

        Parallel.For(0, pairs.Count, new ParallelOptions { MaxDegreeOfParallelism = _workers }, i =>
        {
            winners[i] = PlayBoNParallel(pairs[i].a, pairs[i].b);
        });
        return winners;
    }

    public MatchPoints[] PlaySeriesBatch(IReadOnlyList<(DeckRep a, DeckRep b)> pairs, int gameCount)
    {
        var results = new MatchPoints[pairs.Count];
        if (pairs.Count == 0) return results;
        if (_workers <= 1 || pairs.Count < ParallelPairsThreshold)
        {
            for (int i = 0; i < pairs.Count; i++)
                results[i] = PlaySeriesPoints(pairs[i].a, pairs[i].b, gameCount, useThreadLocalRandom: false);
            return results;
        }

        Parallel.For(0, pairs.Count, new ParallelOptions { MaxDegreeOfParallelism = _workers }, i =>
        {
            results[i] = PlaySeriesPoints(pairs[i].a, pairs[i].b, gameCount, useThreadLocalRandom: true);
        });
        return results;
    }

    public MatchPoints PlaySeriesPoints(DeckRep a, DeckRep b, int gameCount, bool useThreadLocalRandom = false)
    {
        _perf.IncBoSeries();
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        int rounds = Math.Max(1, gameCount);
        double pointsA = 0;
        double pointsB = 0;
        for (int i = 0; i < rounds; i++)
        {
            var r = PlaySingleGameResult(a, b, useThreadLocalRandom, randomJudgeOnAbsoluteDraw: false);
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

    private int PlayBoNParallel(DeckRep a, DeckRep b)
    {
        _perf.IncBoSeries();
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        int need = (_bestOf / 2) + 1;
        int winsA = 0;
        int winsB = 0;
        while (winsA < need && winsB < need)
        {
            int w = PlaySingleGame(a, b, useThreadLocalRandom: true);
            if (w == 0) winsA++;
            else winsB++;
        }
        _perf.AddBoTicks(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
        return winsA > winsB ? 0 : 1;
    }

    private int PlaySingleGame(DeckRep a, DeckRep b, bool useThreadLocalRandom)
    {
        return PlaySingleGameResult(a, b, useThreadLocalRandom, randomJudgeOnAbsoluteDraw: true) switch
        {
            0 => 0,
            1 => 1,
            _ => Next2(useThreadLocalRandom),
        };
    }

    /// <summary>
    /// 返回 0 表示 A 胜，1 表示 B 胜，-1 表示绝对平局（平局且最终生命值相等）。
    /// </summary>
    private int PlaySingleGameResult(DeckRep a, DeckRep b, bool useThreadLocalRandom, bool randomJudgeOnAbsoluteDraw)
    {
        _perf.IncSingleGame();
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        var deckA = ToDeck(a);
        var deckB = ToDeck(b);
        int swap = Next2(useThreadLocalRandom);
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
        else r = randomJudgeOnAbsoluteDraw ? Next2(useThreadLocalRandom) : -1;
        _perf.AddSingleGameTicks(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
        return r;
    }

    private int Next2(bool useThreadLocalRandom)
    {
        if (useThreadLocalRandom)
            return ThreadLocalRandom.Next(2);
        lock (_rng)
            return _rng.Next(2);
    }

    private Deck ToDeck(DeckRep rep)
    {
        var sig = rep.Signature();
        return _deckCache.GetOrAdd(sig, _ =>
        {
            var slots = rep.ItemNames.Select(name => new DeckSlotEntry
            {
                ItemName = name,
                Tier = ItemTier.Bronze,
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
        public void OnCast(BattleItemState caster, string itemName, int timeMs, int? ammoRemainingAfter = null) { }
        public void OnEffect(BattleItemState caster, string itemName, string effectKind, int value, int timeMs, bool isCrit = false, string? extraSuffix = null) { }
        public void OnBurnTick(BattleSide victim, int burnDamage, int remainingBurn, int timeMs) { }
        public void OnPoisonTick(BattleSide victim, int poisonDamage, int timeMs) { }
        public void OnRegenTick(BattleSide side, int heal, int timeMs) { }
        public void OnSandstormTick(int damage, int timeMs) { }
        public void OnResult(int winnerSideIndex, int timeMs, bool isDraw) { }
    }
}
