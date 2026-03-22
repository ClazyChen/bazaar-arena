namespace BazaarArena.GreedyDeckFinder;

/// <summary>瑞士轮与大循环筛选。</summary>
public static class SwissTournament
{
    public static List<CandidateState> RunSwissAndPickTop(
        List<CandidateState> candidates,
        int rounds,
        int topCount,
        BattleEvaluator evaluator,
        Random rng,
        PerfStats perf)
    {
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        foreach (var c in candidates)
        {
            c.SwissScore = 0;
            c.PlayedOpponents.Clear();
        }

        rounds = Math.Max(0, rounds);
        for (int r = 0; r < rounds; r++)
        {
            var groups = candidates
                .GroupBy(x => x.SwissScore)
                .OrderByDescending(g => g.Key)
                .ToList();

            foreach (var group in groups)
            {
                var bucket = group.OrderBy(_ => rng.Next()).ToList();
                var used = new HashSet<string>(StringComparer.Ordinal);
                var matches = new List<(CandidateState a, CandidateState b, bool scoreBoth)>();

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
                        var ghost = opps[rng.Next(opps.Count)];
                        matches.Add((a, ghost, false));
                        continue;
                    }

                    used.Add(b.ComboKey);
                    a.PlayedOpponents.Add(b.ComboKey);
                    b.PlayedOpponents.Add(a.ComboKey);
                    matches.Add((a, b, true));
                }

                if (matches.Count > 0)
                {
                    var pairList = matches.Select(m => (m.a.Representative, m.b.Representative)).ToList();
                    var winners = evaluator.PlayBoNBatch(pairList);
                    for (int i = 0; i < matches.Count; i++)
                    {
                        var match = matches[i];
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
                }
            }
        }

        var result = candidates
            .OrderByDescending(x => x.SwissScore)
            .ThenBy(_ => rng.Next())
            .Take(Math.Min(topCount, candidates.Count))
            .ToList();
        perf.AddSwissTicks(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
        return result;
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
        foreach (var c in candidates) c.RoundRobinScore = 0;
        var pairs = new List<(int i, int j)>();
        for (int i = 0; i < candidates.Count; i++)
        {
            for (int j = i + 1; j < candidates.Count; j++)
            {
                pairs.Add((i, j));
            }
        }

        if (pairs.Count > 0)
        {
            var pairList = pairs.Select(p => (candidates[p.i].Representative, candidates[p.j].Representative)).ToList();
            var points = evaluator.PlaySeriesBatch(pairList, gamesPerPair);
            for (int idx = 0; idx < pairs.Count; idx++)
            {
                var (i, j) = pairs[idx];
                var a = candidates[i];
                var b = candidates[j];
                a.RoundRobinScore += points[idx].A;
                b.RoundRobinScore += points[idx].B;
            }
        }

        var result = candidates
            .OrderByDescending(x => x.RoundRobinScore)
            .ThenByDescending(x => x.SwissScore)
            .ThenBy(_ => rng.Next())
            .Take(Math.Min(topK, candidates.Count))
            .ToList();
        perf.AddRoundRobinTicks(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
        return result;
    }
}
