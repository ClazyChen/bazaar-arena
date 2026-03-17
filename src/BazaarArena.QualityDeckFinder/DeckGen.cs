using BazaarArena.Core;
using BazaarArena.ItemDatabase;

namespace BazaarArena.QualityDeckFinder;

/// <summary>卡组内部表示：形状 + 每槽物品名（无重复）。</summary>
public sealed class DeckRep
{
    public IReadOnlyList<int> Shape { get; }
    public IReadOnlyList<string> ItemNames { get; }

    public DeckRep(IReadOnlyList<int> shape, IReadOnlyList<string> itemNames)
    {
        if (shape.Count != itemNames.Count)
            throw new ArgumentException("形状长度与物品数不一致");
        Shape = shape;
        ItemNames = itemNames;
    }

    /// <summary>稳定签名：形状ID + 槽位物品名拼接，用于 ELO 缓存与去重。</summary>
    public string Signature()
    {
        var shapeId = Shapes.ToDisplayString(Shape);
        return shapeId + "|" + string.Join(",", ItemNames);
    }

    /// <summary>转为 BattleSimulator 使用的 Deck（PlayerLevel=2，铜级）。若物品存在 OverridableAttributes，则 Overrides 设为 Bronze 档默认值的一半（模拟 2 级时局外成长较低）。</summary>
    public Deck ToDeck(IItemTemplateResolver db)
    {
        var slots = new List<DeckSlotEntry>();
        foreach (var name in ItemNames)
        {
            var template = db.GetTemplate(name);
            Dictionary<string, int>? overrides = null;
            if (template?.OverridableAttributes != null)
            {
                overrides = new Dictionary<string, int>();
                foreach (var kv in template.OverridableAttributes)
                {
                    int bronzeVal = template.GetInt(kv.Key, ItemTier.Bronze, 0);
                    overrides[kv.Key] = bronzeVal / 2;
                }
            }
            slots.Add(new DeckSlotEntry
            {
                ItemName = name,
                Tier = ItemTier.Bronze,
                Overrides = overrides,
            });
        }
        return new Deck
        {
            PlayerLevel = 2,
            Slots = slots,
        };
    }

    /// <summary>从 Deck 还原（需已知形状）；若 Deck 来自本探测器则形状一致。</summary>
    public static DeckRep FromDeck(Deck deck, IReadOnlyList<int> shape)
    {
        if (deck.PlayerLevel != 2 || deck.Slots == null)
            throw new ArgumentException("仅支持等级 2 的卡组");
        var names = deck.Slots.Select(s => s.ItemName).ToList();
        return new DeckRep(shape, names);
    }
}

/// <summary>生成无重复随机卡组；Deck ↔ DeckRep 转换。</summary>
public static class DeckGen
{
    /// <summary>在给定形状下从物品池无重复随机抽取，生成一个卡组。若池不足则返回 null。</summary>
    public static DeckRep? RandomDeck(IReadOnlyList<int> shape, ItemPool pool, Random rng)
    {
        if (!pool.CanBuildNoDuplicate(shape))
            return null;
        var used = new HashSet<string>(StringComparer.Ordinal);
        var names = new List<string>(shape.Count);
        for (int i = 0; i < shape.Count; i++)
        {
            var size = shape[i];
            var candidates = pool.NamesForSize(size).Where(n => !used.Contains(n)).ToList();
            if (candidates.Count == 0)
                return null;
            var pick = candidates[rng.Next(candidates.Count)];
            used.Add(pick);
            names.Add(pick);
        }
        return new DeckRep(shape, names);
    }

    /// <summary>
    /// 加权随机生成：以 priors 的物品权重引导抽样，并与均匀探索按 exploreMix 混合。
    /// exploreMix=0 表示完全加权；1 表示完全均匀。
    /// </summary>
    public static DeckRep? RandomDeckWeighted(IReadOnlyList<int> shape, ItemPool pool, Priors priors, Random rng, double exploreMix)
        => RandomDeckWeightedSynergy(
            shape,
            pool,
            priors,
            db: null,
            rng,
            exploreMix,
            pairLambda: 0.0,
            mechanicLambda: 0.0,
            config: new Config(),
            totalGames: 0);

    /// <summary>
    /// 协同加权随机生成：在单物品权重之上叠加“与已选物品集合”的协同加成（物品对 + 机制标签对）。
    /// </summary>
    public static DeckRep? RandomDeckWeightedSynergy(
        IReadOnlyList<int> shape,
        ItemPool pool,
        Priors priors,
        IItemTemplateResolver? db,
        Random rng,
        double exploreMix,
        double pairLambda,
        double mechanicLambda,
        Config config,
        int totalGames)
    {
        exploreMix = Math.Clamp(exploreMix, 0.0, 1.0);
        if (!pool.CanBuildNoDuplicate(shape))
            return null;

        var t = AnnealT(config, totalGames);
        var randMix = Math.Clamp(config.CandidateRandomMixMin, 0.0, 1.0);
        var itemMix = Lerp(Math.Clamp(config.CandidateItemOnlyMixStart, 0.0, 1.0), Math.Clamp(config.CandidateItemOnlyMixEnd, 0.0, 1.0), t);
        if (randMix + itemMix > 1.0)
            itemMix = Math.Max(0.0, 1.0 - randMix);

        var used = new HashSet<string>(StringComparer.Ordinal);
        var names = new List<string>(shape.Count);
        for (int i = 0; i < shape.Count; i++)
        {
            var size = shape[i];
            var candidates = pool.NamesForSize(size).Where(n => !used.Contains(n)).ToList();
            if (candidates.Count == 0)
                return null;

            string pick;
            var roll = rng.NextDouble();
            if (roll < randMix || roll < exploreMix)
            {
                pick = candidates[rng.Next(candidates.Count)];
            }
            else
            {
                bool itemOnly = roll < randMix + itemMix;
                var weights = candidates.Select(n =>
                    CandidateWeight(
                        size,
                        n,
                        names,
                        priors,
                        db,
                        pairLambda,
                        mechanicLambda,
                        config,
                        t,
                        itemOnly)).ToList();
                pick = candidates[WeightedPick.PickIndex(weights, rng)];
            }

            used.Add(pick);
            names.Add(pick);
        }

        return new DeckRep(shape, names);
    }

    /// <summary>
    /// anchored 起点：强制包含 anchor 物品（无重复），并在其余槽位使用协同加权抽样。
    /// anchor 的槽位位置在所有同尺寸槽位中随机选取。
    /// </summary>
    public static DeckRep? RandomDeckWeightedSynergyAnchored(
        IReadOnlyList<int> shape,
        ItemPool pool,
        Priors priors,
        IItemTemplateResolver db,
        Random rng,
        double exploreMix,
        double pairLambda,
        double mechanicLambda,
        string anchorItemName,
        Config config,
        int totalGames)
    {
        if (string.IsNullOrEmpty(anchorItemName)) return null;
        if (!pool.CanBuildNoDuplicate(shape)) return null;

        var anchorTemplate = db.GetTemplate(anchorItemName);
        if (anchorTemplate == null) return null;
        int anchorSize = anchorTemplate.Size switch
        {
            ItemSize.Small => 1,
            ItemSize.Medium => 2,
            ItemSize.Large => 3,
            _ => 0,
        };
        if (anchorSize == 0) return null;

        var positions = new List<int>();
        for (int i = 0; i < shape.Count; i++)
            if (shape[i] == anchorSize) positions.Add(i);
        if (positions.Count == 0) return null;

        int anchorPos = positions[rng.Next(positions.Count)];

        var used = new HashSet<string>(StringComparer.Ordinal) { anchorItemName };
        var names = new List<string>(shape.Count);
        for (int i = 0; i < shape.Count; i++) names.Add("");
        names[anchorPos] = anchorItemName;

        // 按槽位填充；候选权重会自动把 anchor 作为已选集合的一部分，从而偏向其拍档
        var selected = new List<string> { anchorItemName };
        var t = AnnealT(config, totalGames);
        var randMix = Math.Clamp(config.CandidateRandomMixMin, 0.0, 1.0);
        var itemMix = Lerp(Math.Clamp(config.CandidateItemOnlyMixStart, 0.0, 1.0), Math.Clamp(config.CandidateItemOnlyMixEnd, 0.0, 1.0), t);
        if (randMix + itemMix > 1.0)
            itemMix = Math.Max(0.0, 1.0 - randMix);

        for (int i = 0; i < shape.Count; i++)
        {
            if (i == anchorPos) continue;
            int size = shape[i];
            var candidates = pool.NamesForSize(size).Where(n => !used.Contains(n)).ToList();
            if (candidates.Count == 0) return null;

            string pick;
            var roll = rng.NextDouble();
            if (roll < randMix || roll < Math.Clamp(exploreMix, 0.0, 1.0))
            {
                pick = candidates[rng.Next(candidates.Count)];
            }
            else
            {
                bool itemOnly = roll < randMix + itemMix;
                var weights = candidates.Select(n =>
                    CandidateWeight(
                        size,
                        n,
                        selected,
                        priors,
                        db,
                        pairLambda,
                        mechanicLambda,
                        config,
                        t,
                        itemOnly)).ToList();
                pick = candidates[WeightedPick.PickIndex(weights, rng)];
            }

            used.Add(pick);
            names[i] = pick;
            selected.Add(pick);
        }

        return new DeckRep(shape, names);
    }

    private static double CandidateWeight(
        int size,
        string candidate,
        IReadOnlyList<string> selected,
        Priors priors,
        IItemTemplateResolver? db,
        double pairLambda,
        double mechanicLambda,
        Config config,
        double annealT,
        bool itemOnly)
    {
        double itemW = priors.ItemWeight(size, candidate);
        if (itemOnly)
            return Math.Max(1e-6, itemW);

        double w = itemW;

        if (selected.Count == 0 || (pairLambda <= 0 && mechanicLambda <= 0))
            return Math.Max(1e-6, w);

        var clip = Math.Max(1.0, config.PriorsSignalClip);
        var pairEff = Math.Max(0.0, pairLambda * (0.3 + 0.7 * annealT));
        var mechEff = Math.Max(0.0, mechanicLambda * (1.2 - 0.7 * annealT));

        if (pairLambda > 0)
        {
            double sum = 0;
            foreach (var other in selected)
                sum += priors.PairWeight(candidate, other);
            w += pairEff * (sum / clip);
        }

        if (mechanicLambda > 0 && db != null)
        {
            var mechsC = MechanicTagger.GetMechanics(candidate, db);
            if (mechsC.Count > 0)
            {
                double sum = 0;
                foreach (var other in selected)
                {
                    var mechsO = MechanicTagger.GetMechanics(other, db);
                    if (mechsO.Count == 0) continue;
                    foreach (var mc in mechsC)
                        foreach (var mo in mechsO)
                            sum += priors.MechanicPairWeight(mc, mo);
                }
                w += mechEff * (sum / clip);
            }
        }

        return Math.Max(1e-6, w);
    }

    private static double AnnealT(Config config, int totalGames)
    {
        var denom = Math.Max(1, config.PriorsAnnealGames);
        return Math.Clamp(totalGames / (double)denom, 0.0, 1.0);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * Math.Clamp(t, 0.0, 1.0);
}
