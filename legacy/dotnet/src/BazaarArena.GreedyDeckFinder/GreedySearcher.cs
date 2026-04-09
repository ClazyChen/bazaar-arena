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

    /// <summary>单物品起始，等价于 <see cref="Run(IReadOnlyList{string}, Action{int, List{CandidateState}}?)"/> 传入单元素列表。</summary>
    public Dictionary<int, List<CandidateState>> Run(string anchorItem, Action<int, List<CandidateState>>? onSizeCompleted = null) =>
        Run((IReadOnlyList<string>)[anchorItem], onSizeCompleted);

    /// <summary>以有序部分卡组为起点（各物品 size 之和为起始档），其后按 size 递增贪心扩展。</summary>
    public Dictionary<int, List<CandidateState>> Run(
        IReadOnlyList<string> seedOrderedItems,
        Action<int, List<CandidateState>>? onSizeCompleted = null)
    {
        if (seedOrderedItems == null || seedOrderedItems.Count == 0)
            throw new ArgumentException("起始物品列表不能为空。", nameof(seedOrderedItems));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        int anchorSize = 0;
        var orderedNames = new List<string>(seedOrderedItems.Count);
        foreach (var raw in seedOrderedItems)
        {
            var name = raw.Trim();
            if (name.Length == 0)
                continue;
            if (!seen.Add(name))
                throw new InvalidOperationException($"起始卡组含重复物品：{name}");

            int sz = _pool.SizeOfItem(name, _db);
            if (sz <= 0)
                throw new InvalidOperationException($"找不到起始物品或尺寸非法：{name}");
            anchorSize += sz;
            orderedNames.Add(name);
        }

        if (orderedNames.Count == 0)
            throw new ArgumentException("起始物品列表在去掉空项后为空。", nameof(seedOrderedItems));

        int maxSizeSum = Deck.MaxSlotsForLevel(_config.PlayerLevel);
        if (anchorSize > maxSizeSum)
            throw new InvalidOperationException(
                $"起始卡组总占用 {anchorSize} 超过当前等级槽位上限 {maxSizeSum}（玩家等级 {_config.PlayerLevel}）。");

        var topBySize = new Dictionary<int, List<CandidateState>>();
        var initRep = new DeckRep(orderedNames);
        topBySize[anchorSize] =
        [
            new CandidateState
            {
                ComboKey = ComboKeyUtil.BuildComboKey(initRep.ItemNames),
                Representative = initRep,
                SizeSum = anchorSize,
            }
        ];

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
                    long tBucket0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    var used = new HashSet<string>(prev.Representative.ItemNames, StringComparer.Ordinal);
                    var repJobs = new List<List<DeckRep>>();
                    foreach (var item in names)
                    {
                        if (used.Contains(item)) continue;
                        repJobs.Add(BuildInsertionReps(prev.Representative, item));
                    }
                    _perf.AddExpandBucketGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tBucket0);

                    var tRep0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    var reps = RunBatchedKnockoutMany(
                        repJobs,
                        getRep: r => r,
                        onBoCount: count => _perf.AddRepBoSeries(count));
                    _perf.AddRepTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tRep0);

                    long tBucket1 = System.Diagnostics.Stopwatch.GetTimestamp();
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
                    _perf.AddExpandBucketGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tBucket1);
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
            {
                long tPlayoff = System.Diagnostics.Stopwatch.GetTimestamp();
                stage2 = ResolveFinalTopTieByPlayoff(stage2);
                _perf.AddPlayoffTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tPlayoff);
            }
            topBySize[s] = stage2;
            onSizeCompleted?.Invoke(s, stage2);
            if (_config.Perf)
                Console.WriteLine($"[性能] size={s}: 候选={candidates.Count}, 瑞士晋级={stage1.Count}, TopK={stage2.Count}");
        }

        return topBySize;
    }

    private List<CandidateState> ResolveFinalTopTieByPlayoff(List<CandidateState> stage2)
    {
        long tGlue = System.Diagnostics.Stopwatch.GetTimestamp();
        if (stage2.Count <= 1)
        {
            _perf.AddPlayoffGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tGlue);
            return stage2;
        }

        double topScore = stage2.Max(x => x.RoundRobinScore);
        var tied = stage2.Where(x => x.RoundRobinScore == topScore).ToList();
        if (tied.Count <= 1)
        {
            _perf.AddPlayoffGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tGlue);
            return stage2;
        }

        var playoffScore = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var c in tied) playoffScore[c.ComboKey] = 0;

        var pairs = new List<(int i, int j)>();
        for (int i = 0; i < tied.Count; i++)
        {
            for (int j = i + 1; j < tied.Count; j++)
                pairs.Add((i, j));
        }
        _perf.AddPlayoffGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tGlue);

        if (pairs.Count > 0)
        {
            long tSel = System.Diagnostics.Stopwatch.GetTimestamp();
            var pairList = pairs.Select(p => (tied[p.i].Representative, tied[p.j].Representative)).ToList();
            _perf.AddPlayoffGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tSel);
            var points = _evaluator.PlaySeriesBatch(pairList, 20);
            long tGlue2 = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int idx = 0; idx < pairs.Count; idx++)
            {
                var (i, j) = pairs[idx];
                playoffScore[tied[i].ComboKey] += points[idx].A;
                playoffScore[tied[j].ComboKey] += points[idx].B;
            }
            _perf.AddPlayoffGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tGlue2);
        }

        long tGlue3 = System.Diagnostics.Stopwatch.GetTimestamp();
        var tiedOrder = tied
            .OrderByDescending(x => playoffScore[x.ComboKey])
            .ThenByDescending(x => x.SwissScore)
            .ThenBy(_ => _rng.Next())
            .ToList();

        var tiedSet = new HashSet<string>(tied.Select(x => x.ComboKey), StringComparer.Ordinal);
        var others = stage2.Where(x => !tiedSet.Contains(x.ComboKey)).ToList();
        var merged = tiedOrder.Concat(others).ToList();
        _perf.AddPlayoffGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tGlue3);
        return merged;
    }

    private List<CandidateState> ResolveConflictBuckets(Dictionary<string, List<CandidateState>> candidateBuckets)
    {
        long tGlue = System.Diagnostics.Stopwatch.GetTimestamp();
        var singletons = new List<CandidateState>(candidateBuckets.Count);
        var multiBuckets = new List<List<CandidateState>>();
        foreach (var bucket in candidateBuckets.Values)
        {
            if (bucket.Count == 1)
                singletons.Add(bucket[0]);
            else
                multiBuckets.Add(bucket);
        }
        _perf.AddExpandBucketGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tGlue);

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
        long tInit = System.Diagnostics.Stopwatch.GetTimestamp();
        var tournaments = new List<TournamentState<T>>(sources.Count);
        for (int i = 0; i < sources.Count; i++)
        {
            var alive = sources[i].ToList();
            ShuffleInPlace(alive);
            tournaments.Add(new TournamentState<T>(i, alive));
        }
        _perf.AddKnockoutInitGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tInit);

        while (true)
        {
            long tWave0 = System.Diagnostics.Stopwatch.GetTimestamp();
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
            _perf.AddKnockoutWaveGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tWave0);

            if (aliveTournamentCount == 0) break;
            if (pairs.Count > 0)
            {
                onBoCount?.Invoke(pairs.Count);
                var winners = _evaluator.PlayBoNBatch(pairs);
                long tWave1 = System.Diagnostics.Stopwatch.GetTimestamp();
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
                _perf.AddKnockoutWaveGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tWave1);
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
