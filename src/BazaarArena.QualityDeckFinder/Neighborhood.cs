namespace BazaarArena.QualityDeckFinder;

/// <summary>
/// 邻域：\n
/// - 组合邻域（外层搜索）：仅“同尺寸替换”产生新组合（不做交换）。\n
/// - 排列邻域（组合内部）：仅“交换两个位置”用于寻找代表排列。\n
/// </summary>
public static class Neighborhood
{
    /// <summary>
    /// 枚举组合邻居：对每个槽位做“同尺寸替换”（换同尺寸池中未出现的其它物品）。
    /// 返回 (comboSig, seedRepresentative)；同一 comboSig 可能由不同槽位替换产生，需去重。
    /// </summary>
    public static List<(string comboSig, DeckRep seedRepresentative)> EnumerateComboNeighbors(DeckRep representative, ItemPool pool)
    {
        var list = new List<(string, DeckRep)>();
        var shape = representative.Shape;
        var names = representative.ItemNames.ToList();
        var used = new HashSet<string>(representative.ItemNames, StringComparer.Ordinal);

        for (int slot = 0; slot < shape.Count; slot++)
        {
            var size = shape[slot];
            var current = names[slot];
            foreach (var name in pool.NamesForSize(size))
            {
                if (name == current) continue;
                if (used.Contains(name)) continue;
                var next = new List<string>(names) { [slot] = name };
                var seed = new DeckRep(shape, next);
                var comboSig = ComboSignature.FromDeckRep(seed);
                list.Add((comboSig, seed));
            }
        }
        return list;
    }

    /// <summary>随机采样最多 sampleSize 个组合邻居，不重复（按 comboSig 去重）。</summary>
    public static List<(string comboSig, DeckRep seedRepresentative)> SampleComboNeighbors(DeckRep representative, ItemPool pool, Random rng, int sampleSize)
    {
        var all = EnumerateComboNeighbors(representative, pool);
        if (all.Count == 0) return [];
        // 先洗牌，再按 comboSig 去重取前 sampleSize
        var shuffled = all.OrderBy(_ => rng.Next()).ToList();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<(string, DeckRep)>(Math.Min(sampleSize, shuffled.Count));
        foreach (var x in shuffled)
        {
            if (!seen.Add(x.comboSig)) continue;
            result.Add(x);
            if (result.Count >= sampleSize) break;
        }
        return result;
    }

    /// <summary>
    /// 加权采样组合邻居：对“替换进来的物品”按 priors 权重采样，并与均匀探索按 exploreMix 混合。
    /// - exploreMix=1：完全均匀（等同 SampleComboNeighbors）
    /// - exploreMix=0：完全加权
    /// 同一 comboSig 若有多条 seed（理论上较少），保留权重更高的一条 seed 作为代表。
    /// </summary>
    public static List<(string comboSig, DeckRep seedRepresentative)> SampleComboNeighborsWeighted(
        DeckRep representative,
        ItemPool pool,
        Priors priors,
        BazaarArena.ItemDatabase.IItemTemplateResolver? db,
        Random rng,
        int sampleSize,
        double exploreMix,
        double pairLambda = 0.0,
        string? anchorItemName = null,
        Config? config = null,
        int totalGames = 0)
    {
        exploreMix = Math.Clamp(exploreMix, 0.0, 1.0);
        config ??= new Config();
        var t = Math.Clamp(totalGames / (double)Math.Max(1, config.PriorsAnnealGames), 0.0, 1.0);
        var clip = Math.Max(1.0, config.PriorsSignalClip);
        var itemOnlyMix = Math.Clamp(
            config.CandidateItemOnlyMixStart + (config.CandidateItemOnlyMixEnd - config.CandidateItemOnlyMixStart) * t,
            0.0,
            1.0);
        var pairEff = Math.Max(0.0, pairLambda * (0.3 + 0.7 * t));

        var shape = representative.Shape;
        var names = representative.ItemNames.ToList();
        var used = new HashSet<string>(representative.ItemNames, StringComparer.Ordinal);

        // comboSig -> (seed, weight)
        var best = new Dictionary<string, (DeckRep seed, double w)>(StringComparer.Ordinal);

        for (int slot = 0; slot < shape.Count; slot++)
        {
            var size = shape[slot];
            var current = names[slot];
            if (!string.IsNullOrEmpty(anchorItemName) && current == anchorItemName)
                continue;
            foreach (var name in pool.NamesForSize(size))
            {
                if (name == current) continue;
                if (used.Contains(name)) continue;
                var next = new List<string>(names) { [slot] = name };
                var seed = new DeckRep(shape, next);
                var comboSig = ComboSignature.FromDeckRep(seed);

                // 权重：单物品权重 + 物品对协同 + 声明协同先验得分
                double itemW = priors.ItemWeight(size, name);
                double comboW = itemW;
                if (pairLambda > 0)
                {
                    double pairSum = 0;
                    foreach (var other in names)
                    {
                        if (other == current) continue;
                        pairSum += priors.PairWeight(name, other);
                    }
                    comboW += pairEff * (pairSum / clip);
                }
                if (db != null)
                {
                    var templateX = db.GetTemplate(name);
                    var synergyScore = SynergyScorer.Score(templateX, representative, slot, db);
                    comboW += Math.Max(0, synergyScore);
                }

                // 与单物品模式做混合（退火）：早期更接近 itemW，后期更接近 comboW。
                var w = Math.Max(1e-6, (1.0 - itemOnlyMix) * comboW + itemOnlyMix * itemW);
                if (!best.TryGetValue(comboSig, out var ex) || w > ex.w)
                    best[comboSig] = (seed, w);
            }
        }

        if (best.Count == 0) return [];
        if (best.Count <= sampleSize) return best.Select(kv => (kv.Key, kv.Value.seed)).ToList();

        // 混合探索：先决定每个样本是“均匀”还是“加权”
        var result = new List<(string, DeckRep)>(sampleSize);
        var remaining = best.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        for (int pick = 0; pick < sampleSize && remaining.Count > 0; pick++)
        {
            bool uniform = rng.NextDouble() < exploreMix;
            string chosenSig;
            if (uniform)
            {
                // 从 remaining 中均匀抽一个
                var idx = rng.Next(remaining.Count);
                chosenSig = remaining.Keys.ElementAt(idx);
            }
            else
            {
                // 无放回加权抽样：用 Efraimidis-Spirakis 的“随机键”近似实现，每次取最大键
                // key = U^(1/w)，w 越大 key 越接近 1 更容易胜出
                chosenSig = "";
                double bestKey = double.NegativeInfinity;
                foreach (var kv in remaining)
                {
                    var w = Math.Max(1e-6, kv.Value.w);
                    var u = Math.Max(1e-12, rng.NextDouble());
                    var key = Math.Pow(u, 1.0 / w);
                    if (key > bestKey)
                    {
                        bestKey = key;
                        chosenSig = kv.Key;
                    }
                }
            }

            var chosen = remaining[chosenSig];
            result.Add((chosenSig, chosen.seed));
            remaining.Remove(chosenSig);
        }

        return result;
    }
}
