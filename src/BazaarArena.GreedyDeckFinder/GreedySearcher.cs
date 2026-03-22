using BazaarArena.Core;
using BazaarArena.ItemDatabase;

namespace BazaarArena.GreedyDeckFinder;

/// <summary>按物理占用 size 递增的锚定贪心搜索。</summary>
public sealed class GreedySearcher
{
    private readonly Config _config;
    private readonly ItemPool _pool;
    private readonly IItemTemplateResolver _db;
    private readonly BattleEvaluator _evaluator;
    private readonly Random _rng;
    private readonly PerfStats _perf;

    public GreedySearcher(
        Config config,
        ItemPool pool,
        IItemTemplateResolver db,
        BattleEvaluator evaluator,
        Random rng,
        PerfStats perf)
    {
        _config = config;
        _pool = pool;
        _db = db;
        _evaluator = evaluator;
        _rng = rng;
        _perf = perf;
    }

    public Dictionary<int, List<CandidateState>> Run(string anchorItem, Action<int, List<CandidateState>>? onSizeCompleted = null)
    {
        int anchorSize = _pool.SizeOfItem(anchorItem, _db);
        if (anchorSize <= 0)
            throw new InvalidOperationException($"找不到锚定物品或尺寸非法：{anchorItem}");

        var topBySize = new Dictionary<int, List<CandidateState>>();
        var initRep = new DeckRep([anchorItem]);
        topBySize[anchorSize] =
        [
            new CandidateState
            {
                ComboKey = ComboKeyUtil.BuildComboKey(initRep.ItemNames),
                Representative = initRep,
                SizeSum = anchorSize,
            }
        ];

        int maxSizeSum = Deck.MaxSlotsForLevel(_config.PlayerLevel);
        for (int s = anchorSize + 1; s <= maxSizeSum; s++)
        {
            long tExpand0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var candidateBuckets = new Dictionary<string, List<CandidateState>>(StringComparer.Ordinal);
            for (int q = 1; q <= 3; q++)
            {
                int p = s - q;
                if (p < anchorSize || !topBySize.TryGetValue(p, out var prevTop))
                    continue;

                var names = _pool.NamesForSize(q);
                foreach (var prev in prevTop)
                {
                    var used = new HashSet<string>(prev.Representative.ItemNames, StringComparer.Ordinal);
                    var repJobs = new List<List<DeckRep>>();
                    foreach (var item in names)
                    {
                        if (used.Contains(item)) continue;
                        repJobs.Add(BuildInsertionReps(prev.Representative, item));
                    }
                    var tRep0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    var reps = RunBatchedKnockoutMany(
                        repJobs,
                        getRep: r => r,
                        onBoCount: count => _perf.AddRepBoSeries(count));
                    _perf.AddRepTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tRep0);

                    foreach (var rep in reps)
                    {
                        var key = ComboKeyUtil.BuildComboKey(rep.ItemNames);
                        var state = new CandidateState
                        {
                            ComboKey = key,
                            Representative = rep,
                            SizeSum = s,
                        };
                        if (!candidateBuckets.TryGetValue(key, out var list))
                            candidateBuckets[key] = [state];
                        else
                            list.Add(state);
                    }
                }
            }

            var candidates = ResolveConflictBuckets(candidateBuckets);
            if (candidates.Count == 0)
                throw new InvalidOperationException($"size={s} 未生成任何候选。");
            _perf.AddExpandTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tExpand0);

            int rounds = (int)Math.Ceiling(Math.Log2(Math.Max(1, candidates.Count)));
            int topKm = Math.Min(candidates.Count, _config.TopK * _config.TopMultiplier);
            var stage1 = SwissTournament.RunSwissAndPickTop(candidates, rounds, topKm, _evaluator, _rng, _perf);
            var stage2 = SwissTournament.RunRoundRobinAndPickTop(stage1, _config.TopK, _config.BestOf, _evaluator, _rng, _perf);
            if (s == maxSizeSum)
                stage2 = ResolveFinalTopTieByPlayoff(stage2);
            topBySize[s] = stage2;
            onSizeCompleted?.Invoke(s, stage2);
            if (_config.Perf)
                Console.WriteLine($"[性能] size={s}: 候选={candidates.Count}, 瑞士晋级={stage1.Count}, TopK={stage2.Count}");
        }

        return topBySize;
    }

    private List<CandidateState> ResolveFinalTopTieByPlayoff(List<CandidateState> stage2)
    {
        if (stage2.Count <= 1) return stage2;
        double topScore = stage2.Max(x => x.RoundRobinScore);
        var tied = stage2.Where(x => x.RoundRobinScore == topScore).ToList();
        if (tied.Count <= 1) return stage2;

        var playoffScore = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var c in tied) playoffScore[c.ComboKey] = 0;

        var pairs = new List<(int i, int j)>();
        for (int i = 0; i < tied.Count; i++)
        {
            for (int j = i + 1; j < tied.Count; j++)
                pairs.Add((i, j));
        }

        if (pairs.Count > 0)
        {
            var pairList = pairs.Select(p => (tied[p.i].Representative, tied[p.j].Representative)).ToList();
            var points = _evaluator.PlaySeriesBatch(pairList, 20);
            for (int idx = 0; idx < pairs.Count; idx++)
            {
                var (i, j) = pairs[idx];
                playoffScore[tied[i].ComboKey] += points[idx].A;
                playoffScore[tied[j].ComboKey] += points[idx].B;
            }
        }

        var tiedOrder = tied
            .OrderByDescending(x => playoffScore[x.ComboKey])
            .ThenByDescending(x => x.SwissScore)
            .ThenBy(_ => _rng.Next())
            .ToList();

        var tiedSet = new HashSet<string>(tied.Select(x => x.ComboKey), StringComparer.Ordinal);
        var others = stage2.Where(x => !tiedSet.Contains(x.ComboKey)).ToList();
        return tiedOrder.Concat(others).ToList();
    }

    private List<CandidateState> ResolveConflictBuckets(Dictionary<string, List<CandidateState>> candidateBuckets)
    {
        var singletons = new List<CandidateState>(candidateBuckets.Count);
        var multiBuckets = new List<List<CandidateState>>();
        foreach (var bucket in candidateBuckets.Values)
        {
            if (bucket.Count == 1)
                singletons.Add(bucket[0]);
            else
                multiBuckets.Add(bucket);
        }

        if (multiBuckets.Count == 0) return singletons;
        var winners = RunBatchedKnockoutMany(
            multiBuckets,
            getRep: c => c.Representative);
        singletons.AddRange(winners);
        return singletons;
    }

    private List<DeckRep> BuildInsertionReps(DeckRep prevRep, string newItem)
    {
        var reps = new List<DeckRep>(prevRep.ItemNames.Count + 1);
        for (int pos = 0; pos <= prevRep.ItemNames.Count; pos++)
        {
            var items = prevRep.ItemNames.ToList();
            items.Insert(pos, newItem);
            reps.Add(new DeckRep(items));
        }
        _perf.AddRepCandidates(reps.Count);
        return reps;
    }

    private List<T> RunBatchedKnockoutMany<T>(
        IReadOnlyList<List<T>> sources,
        Func<T, DeckRep> getRep,
        Action<int>? onBoCount = null)
    {
        var tournaments = new List<TournamentState<T>>(sources.Count);
        for (int i = 0; i < sources.Count; i++)
        {
            var alive = sources[i].ToList();
            ShuffleInPlace(alive);
            tournaments.Add(new TournamentState<T>(i, alive));
        }

        while (true)
        {
            var pairs = new List<(DeckRep a, DeckRep b)>();
            var pairMembers = new List<(int tIndex, T a, T b)>();
            int aliveTournamentCount = 0;
            foreach (var t in tournaments)
            {
                if (t.Alive.Count <= 1) continue;
                aliveTournamentCount++;
                for (int i = 0; i + 1 < t.Alive.Count; i += 2)
                {
                    var a = t.Alive[i];
                    var b = t.Alive[i + 1];
                    pairMembers.Add((t.Index, a, b));
                    pairs.Add((getRep(a), getRep(b)));
                }
            }

            if (aliveTournamentCount == 0) break;
            if (pairs.Count > 0)
            {
                onBoCount?.Invoke(pairs.Count);
                var winners = _evaluator.PlayBoNBatch(pairs);
                var nextByTournament = new List<T>[tournaments.Count];
                for (int i = 0; i < nextByTournament.Length; i++)
                    nextByTournament[i] = [];
                for (int i = 0; i < winners.Length; i++)
                {
                    var (tIndex, a, b) = pairMembers[i];
                    nextByTournament[tIndex].Add(winners[i] == 0 ? a : b);
                }
                foreach (var t in tournaments)
                {
                    if (t.Alive.Count <= 1) continue;
                    if ((t.Alive.Count & 1) == 1)
                        nextByTournament[t.Index].Add(t.Alive[^1]);
                    t.Alive = nextByTournament[t.Index];
                }
            }
        }

        var result = new T[tournaments.Count];
        foreach (var t in tournaments)
            result[t.Index] = t.Alive[0];
        return result.ToList();
    }

    private void ShuffleInPlace<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private sealed class TournamentState<T>(int index, List<T> alive)
    {
        public int Index { get; } = index;
        public List<T> Alive { get; set; } = alive;
    }
}
