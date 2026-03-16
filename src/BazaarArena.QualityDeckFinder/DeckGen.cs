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

    /// <summary>转为 BattleSimulator 使用的 Deck（PlayerLevel=2，铜级，无 Overrides）。</summary>
    public Deck ToDeck()
    {
        var slots = ItemNames.Select(name => new DeckSlotEntry
        {
            ItemName = name,
            Tier = ItemTier.Bronze,
            Overrides = null,
        }).ToList();
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
}
