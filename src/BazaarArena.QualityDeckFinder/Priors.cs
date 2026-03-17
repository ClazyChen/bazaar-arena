using BazaarArena.Core;
using BazaarArena.ItemDatabase;

namespace BazaarArena.QualityDeckFinder;

/// <summary>
/// 在线学习的“强模式”先验：\n
/// - 形状计数（小/中/大槽位数量）的权重\n
/// - 各尺寸物品的权重\n
/// 采用指数滑动平均（EMA），并支持与均匀采样混合以保持探索。
/// </summary>
public sealed class Priors
{
    public Dictionary<string, double> ShapeCountWeights { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, double> ItemWeights { get; } = new(StringComparer.Ordinal); // key: $"{size}:{itemName}"

    /// <summary>物品对协同权重（无序对）：key = "A|B"（按 Ordinal 排序）。</summary>
    public Dictionary<string, double> PairWeights { get; } = new(StringComparer.Ordinal);

    public double EmaAlpha { get; set; } = 0.08;

    public void ObserveCombo(
        DeckRep representative,
        double elo,
        Config config,
        IItemTemplateResolver db,
        bool isConfirmed,
        int gameCount)
    {
        // 学习信号：允许为负，用于后续纠偏；再通过裁剪与样本可信度降权，避免早期噪声带偏全局。
        var baseline = config.InitialElo;
        var signalRaw = elo - baseline;
        var clip = Math.Max(1.0, config.PriorsSignalClip);
        var signal = Math.Clamp(signalRaw, -clip, clip);

        var w = SampleTrustWeight(isConfirmed, gameCount, config);
        var effectiveAlpha = Math.Clamp(EmaAlpha, 0.0, 1.0) * w;
        if (effectiveAlpha <= 0)
            return;

        var itemNames = representative.ItemNames.ToList();

        foreach (var name in itemNames)
        {
            var t = db.GetTemplate(name);
            if (t == null) continue;
            var size = t.Size switch
            {
                ItemSize.Small => 1,
                ItemSize.Medium => 2,
                ItemSize.Large => 3,
                _ => 0,
            };
            if (size == 0) continue;
            var key = $"{size}:{name}";
            UpdateEma(ItemWeights, key, signal, effectiveAlpha);
        }

        // 物品对协同：组合内任意两件物品共同出现即记忆（无序）。
        for (int i = 0; i < itemNames.Count; i++)
        {
            for (int j = i + 1; j < itemNames.Count; j++)
            {
                var pairKey = MakePairKey(itemNames[i], itemNames[j]);
                UpdateEma(PairWeights, pairKey, signal, effectiveAlpha);
            }
        }
    }

    public double ShapeWeight((int small, int medium, int large) counts)
    {
        var key = $"c={counts.small},{counts.medium},{counts.large}";
        // 形状学习将逐步废弃；保留读取以兼容旧状态文件。
        return ShapeCountWeights.TryGetValue(key, out var w) ? Math.Max(1e-6, w) : 1.0;
    }

    public double ItemWeight(int size, string name)
    {
        var key = $"{size}:{name}";
        // dict 内存的是“分数”（可正可负），这里转成采样权重（必须为正）。
        // scale 越大，分数对权重影响越温和。
        var scale = Math.Max(1.0, PriorsItemScoreScale);
        var score = ItemWeights.TryGetValue(key, out var s) ? s : 0.0;
        return Math.Max(1e-6, 1.0 + score / scale);
    }

    public double PairWeight(string a, string b)
    {
        var key = MakePairKey(a, b);
        return PairWeights.TryGetValue(key, out var w) ? w : 0.0;
    }

    private static string MakePairKey(string a, string b)
    {
        if (string.CompareOrdinal(a, b) <= 0)
            return a + "|" + b;
        return b + "|" + a;
    }

    internal const double PriorsItemScoreScale = 200.0;

    private static double SampleTrustWeight(bool isConfirmed, int gameCount, Config config)
    {
        var wConfirm = isConfirmed ? 1.0 : Math.Clamp(config.PriorsUnconfirmedMultiplier, 0.0, 1.0);
        var full = Math.Max(1, config.PriorsMinGamesForFullWeight);
        var wGames = Math.Clamp(gameCount / (double)full, 0.0, 1.0);
        return wConfirm * wGames;
    }

    private static void UpdateEma(Dictionary<string, double> dict, string key, double value, double alpha)
    {
        var a = Math.Clamp(alpha, 0.0, 1.0);
        if (!dict.TryGetValue(key, out var old))
        {
            dict[key] = value;
            return;
        }
        dict[key] = (1 - a) * old + a * value;
    }
}

internal static class WeightedPick
{
    public static int PickIndex(IReadOnlyList<double> weights, Random rng)
    {
        double sum = 0;
        for (int i = 0; i < weights.Count; i++) sum += Math.Max(0, weights[i]);
        if (sum <= 0) return rng.Next(weights.Count);
        var r = rng.NextDouble() * sum;
        double acc = 0;
        for (int i = 0; i < weights.Count; i++)
        {
            acc += Math.Max(0, weights[i]);
            if (r <= acc) return i;
        }
        return weights.Count - 1;
    }
}

