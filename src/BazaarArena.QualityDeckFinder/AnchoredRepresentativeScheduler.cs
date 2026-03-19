namespace BazaarArena.QualityDeckFinder;

/// <summary>
/// 每赛季每物品选一名锚定代表：在该物品的所有 (item, shape) 锚定玩家中按权重抽样（高分更易被选，保留探索）。
/// </summary>
public static class AnchoredRepresentativeScheduler
{
    /// <summary>
    /// 从 state.AnchoredPlayerComboSig 中按物品分组，每物品选一名代表（anchored key）。
    /// 返回：itemName -> 本季代表对应的 anchored key（"itemName|shapeIndex"）。
    /// </summary>
    public static Dictionary<string, string> SelectRepresentatives(OptimizerState state, Config config, Random rng)
    {
        var byItem = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var key in state.AnchoredPlayerComboSig.Keys)
        {
            var itemName = ItemNameFromKey(key);
            if (string.IsNullOrEmpty(itemName)) continue;
            if (!byItem.TryGetValue(itemName, out var list))
            {
                list = new List<string>();
                byItem[itemName] = list;
            }
            list.Add(key);
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var baseline = config.InitialElo;
        var temp = Math.Max(1e-6, config.RepresentativeTemperature);
        var exploreProb = Math.Clamp(config.RepresentativeExploreProb, 0.0, 1.0);
        var minGames = Math.Max(0, config.MinGamesForRepresentative);

        foreach (var kv in byItem)
        {
            var itemName = kv.Key;
            var keys = kv.Value;
            if (keys.Count == 0) continue;
            if (keys.Count == 1)
            {
                result[itemName] = keys[0];
                continue;
            }

            string chosen;
            if (rng.NextDouble() < exploreProb)
            {
                chosen = keys[rng.Next(keys.Count)];
            }
            else
            {
                var weights = new List<double>(keys.Count);
                foreach (var key in keys)
                {
                    if (!state.AnchoredPlayerComboSig.TryGetValue(key, out var comboSig) ||
                        !state.VirtualPlayerPool.TryGetValue(comboSig, out var entry))
                    {
                        weights.Add(1.0);
                        continue;
                    }
                    var elo = entry.Elo;
                    var games = entry.GameCount;
                    // 基础：softmax(ELO)
                    var w = Math.Exp((elo - baseline) / temp);

                    // 公平性：对局数不足的 shape 需要“补课”，否则很容易出现 0 对局形状长期没有参赛机会。
                    // minGames>0 时启用：games 越小 bonus 越大；达到 minGames 后 bonus=1。
                    if (minGames > 0 && games < minGames)
                    {
                        var deficit = (minGames - games) / (double)minGames; // 0~1
                        var bonus = 1.0 + 4.0 * deficit; // 1~5
                        w *= bonus;
                    }

                    // 公平性：被选为代表次数越少，越应提高抽中概率（防止某个 shape 在高 ELO 后长期垄断）
                    int picks = state.AnchoredPickCounts.TryGetValue(key, out var pc) ? pc : 0;
                    w *= 1.0 + (2.0 / (picks + 1.0)); // picks=0 -> 3x, picks=1 -> 2x, picks=2 -> 1.67x...
                    weights.Add(Math.Max(1e-6, w));
                }
                chosen = keys[WeightedPick.PickIndex(weights, rng)];
            }
            result[itemName] = chosen;

            // 记录参赛次数（仅对最终 chosen 计数；探索分支也计数，保证机会均衡）
            state.AnchoredPickCounts.AddOrUpdate(chosen, 1, (_, v) => v + 1);
        }

        return result;
    }

    /// <summary>从 anchored key "itemName|shapeIndex" 解析出物品名。</summary>
    public static string? ItemNameFromKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        var last = key.LastIndexOf('|');
        if (last <= 0) return null;
        return key.Substring(0, last);
    }

    /// <summary>从 anchored key 解析出形状索引。</summary>
    public static int ShapeIndexFromKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return 0;
        var last = key.LastIndexOf('|');
        if (last < 0 || last == key.Length - 1) return 0;
        return int.TryParse(key.Substring(last + 1), out var idx) ? idx : 0;
    }
}
