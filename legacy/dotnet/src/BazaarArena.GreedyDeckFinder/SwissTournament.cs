namespace BazaarArena.GreedyDeckFinder;

/// <summary>瑞士轮与大循环筛选。</summary>
public static class SwissTournament
{
    /// <summary>
    /// 瑞士轮结束后按 <see cref="CandidateState.SwissScore"/> 取前 <paramref name="topCount"/> 名（同分随机序）。
    /// 每轮开始前剪枝：若某候选当前分 + 剩余轮数（每轮至多 +1）仍严格低于至少 <paramref name="topCount"/> 名其他选手的<strong>当前</strong>分，
    /// 则其最终名次不可能进入前 <paramref name="topCount"/>，不再参与后续配对与对战。
    /// 若存活人数已不超过 <paramref name="topCount"/>，提前结束瑞士轮。
    /// </summary>
    /// <param name="mainRng">主搜索随机源；进入瑞士时从中取 1 个 <see cref="Random.Next()"/> 作为瑞士子流种子，瑞士内配对/同分乱序仅消耗该子流，不因瑞士轮数或剪枝改变主源消耗节奏。</param>
    public static List<CandidateState> RunSwissAndPickTop(
        List<CandidateState> candidates,
        int rounds,
        int topCount,
        BattleEvaluator evaluator,
        Random mainRng,
        PerfStats perf)
    {
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        long tGlue = System.Diagnostics.Stopwatch.GetTimestamp();
        foreach (var c in candidates)
        {
            c.SwissScore = 0;
            c.PlayedOpponents.Clear();
        }

        int swissSeed;
        lock (mainRng)
            swissSeed = mainRng.Next();
        var swissRng = new Random(swissSeed);

        // 浅拷贝：与 candidates 共享 CandidateState；仅从 active 移除已剪枝项，不修改调用方列表长度（便于日志打印初始候选数）。
        var active = new List<CandidateState>(candidates);
        perf.AddSwissGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tGlue);

        rounds = Math.Max(0, rounds);
        for (int r = 0; r < rounds; r++)
        {
            tGlue = System.Diagnostics.Stopwatch.GetTimestamp();
            int remainingInclusive = rounds - r;
            int pruned = RemoveSwissImpossible(active, topCount, remainingInclusive);
            if (pruned > 0)
                perf.AddSwissPruned(pruned);

            if (active.Count <= topCount)
            {
                perf.AddSwissGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tGlue);
                break;
            }

            var groups = active
                .GroupBy(x => x.SwissScore)
                .OrderByDescending(g => g.Key)
                .ToList();

            // 整轮所有分数桶的对局合并为一次 PlayBoNBatch，便于多核对战饱和（此前按桶调用时每批常不足并行阈值）。
            var roundMatches = new List<(CandidateState a, CandidateState b, bool scoreBoth)>(Math.Max(4, active.Count / 2));
            foreach (var group in groups)
            {
                var bucket = group.OrderBy(_ => swissRng.Next()).ToList();
                var used = new HashSet<string>(StringComparer.Ordinal);

                for (int i = 0; i < bucket.Count; i++)
                {
                    var a = bucket[i];
                    if (!used.Add(a.ComboKey)) continue;

                    CandidateState? b = null;
                    for (int j = i + 1; j < bucket.Count; j++)
                    {
                        var x = bucket[j];
                        if (used.Contains(x.ComboKey)) continue;
                        if (a.PlayedOpponents.Contains(x.ComboKey)) continue;
                        b = x;
                        break;
                    }

                    if (b == null)
                    {
                        // 桶内奇数：随机挑同桶对手，仅计当前候选得分，不计对手得分。
                        var opps = bucket.Where(x => x.ComboKey != a.ComboKey).ToList();
                        if (opps.Count == 0) continue;
                        var ghost = opps[swissRng.Next(opps.Count)];
                        roundMatches.Add((a, ghost, false));
                        continue;
                    }

                    used.Add(b.ComboKey);
                    a.PlayedOpponents.Add(b.ComboKey);
                    b.PlayedOpponents.Add(a.ComboKey);
                    roundMatches.Add((a, b, true));
                }
            }

            perf.AddSwissGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tGlue);

            if (roundMatches.Count > 0)
            {
                tGlue = System.Diagnostics.Stopwatch.GetTimestamp();
                var pairList = roundMatches.Select(m => (m.a.Representative, m.b.Representative)).ToList();
                perf.AddSwissGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tGlue);
                var winners = evaluator.PlayBoNBatch(pairList);
                tGlue = System.Diagnostics.Stopwatch.GetTimestamp();
                for (int i = 0; i < roundMatches.Count; i++)
                {
                    var match = roundMatches[i];
                    int win = winners[i];
                    if (match.scoreBoth)
                    {
                        if (win == 0) match.a.SwissScore += 1;
                        else match.b.SwissScore += 1;
                    }
                    else
                    {
                        if (win == 0) match.a.SwissScore += 1;
                    }
                }
                perf.AddSwissGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tGlue);
            }
        }

        tGlue = System.Diagnostics.Stopwatch.GetTimestamp();
        var result = active
            .OrderByDescending(x => x.SwissScore)
            .ThenBy(_ => swissRng.Next())
            .Take(Math.Min(topCount, active.Count))
            .ToList();
        perf.AddSwissGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tGlue);
        perf.AddSwissTicks(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
        return result;
    }

    /// <summary>
    /// 若某候选理论最高终分 = 当前分 + <paramref name="remainingRounds"/>（余下每轮全胜），
    /// 仍低于至少 <paramref name="topCount"/> 名其他选手的当前分，则不可能进入瑞士结束后的前 <paramref name="topCount"/> 名。
    /// </summary>
    private static int RemoveSwissImpossible(List<CandidateState> active, int topCount, int remainingRounds)
    {
        if (active.Count <= topCount || topCount <= 0)
            return 0;

        var removeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in active)
        {
            double maxFinal = c.SwissScore + Math.Max(0, remainingRounds);
            int strictlyAhead = 0;
            foreach (var d in active)
            {
                if (ReferenceEquals(d, c)) continue;
                if (d.SwissScore > maxFinal)
                    strictlyAhead++;
            }

            if (strictlyAhead >= topCount)
                removeKeys.Add(c.ComboKey);
        }

        if (removeKeys.Count == 0)
            return 0;

        return active.RemoveAll(c => removeKeys.Contains(c.ComboKey));
    }

    public static List<CandidateState> RunRoundRobinAndPickTop(
        List<CandidateState> candidates,
        int topK,
        int gamesPerPair,
        BattleEvaluator evaluator,
        Random rng,
        PerfStats perf)
    {
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        long tGlue = System.Diagnostics.Stopwatch.GetTimestamp();
        foreach (var c in candidates) c.RoundRobinScore = 0;
        var pairs = new List<(int i, int j)>();
        for (int i = 0; i < candidates.Count; i++)
        {
            for (int j = i + 1; j < candidates.Count; j++)
            {
                pairs.Add((i, j));
            }
        }
        perf.AddRoundRobinGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tGlue);

        if (pairs.Count > 0)
        {
            tGlue = System.Diagnostics.Stopwatch.GetTimestamp();
            var pairList = pairs.Select(p => (candidates[p.i].Representative, candidates[p.j].Representative)).ToList();
            perf.AddRoundRobinGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tGlue);
            var points = evaluator.PlaySeriesBatch(pairList, gamesPerPair);
            tGlue = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int idx = 0; idx < pairs.Count; idx++)
            {
                var (i, j) = pairs[idx];
                var a = candidates[i];
                var b = candidates[j];
                a.RoundRobinScore += points[idx].A;
                b.RoundRobinScore += points[idx].B;
            }
            perf.AddRoundRobinGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tGlue);
        }

        tGlue = System.Diagnostics.Stopwatch.GetTimestamp();
        var result = candidates
            .OrderByDescending(x => x.RoundRobinScore)
            .ThenByDescending(x => x.SwissScore)
            .ThenBy(_ => rng.Next())
            .Take(Math.Min(topK, candidates.Count))
            .ToList();
        perf.AddRoundRobinGlueTicks(System.Diagnostics.Stopwatch.GetTimestamp() - tGlue);
        perf.AddRoundRobinTicks(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
        return result;
    }
}
