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

    public double EmaAlpha { get; set; } = 0.08;

    public void ObserveCombo(DeckRep representative, double elo, Config config, IItemTemplateResolver db)
    {
        // 以 (elo - 初始分) 作为信号；低于初始分不强行拉低（避免噪声导致权重塌陷）
        var delta = Math.Max(0.0, elo - config.InitialElo);
        var counts = ComboSignature.ShapeCounts(representative.Shape);
        var shapeKey = $"c={counts.small},{counts.medium},{counts.large}";
        UpdateEma(ShapeCountWeights, shapeKey, delta);

        foreach (var name in representative.ItemNames)
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
            UpdateEma(ItemWeights, key, delta);
        }
    }

    public double ShapeWeight((int small, int medium, int large) counts)
    {
        var key = $"c={counts.small},{counts.medium},{counts.large}";
        return ShapeCountWeights.TryGetValue(key, out var w) ? Math.Max(1e-6, w) : 1.0;
    }

    public double ItemWeight(int size, string name)
    {
        var key = $"{size}:{name}";
        return ItemWeights.TryGetValue(key, out var w) ? Math.Max(1e-6, w) : 1.0;
    }

    private void UpdateEma(Dictionary<string, double> dict, string key, double value)
    {
        var a = Math.Clamp(EmaAlpha, 0.0, 1.0);
        if (!dict.TryGetValue(key, out var old))
        {
            dict[key] = Math.Max(1e-6, value);
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

