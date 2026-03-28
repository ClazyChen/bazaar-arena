using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using ItemDb = BazaarArena.ItemDatabase.ItemDatabase;

namespace BazaarArena.GreedyDeckFinder;

/// <summary>海盗（Vanessa）最新版物品池（<see cref="GreedyLevelRules.IsMinTierAllowedInPool"/> 决定可选 MinTier）。</summary>
public sealed class ItemPool
{
    public IReadOnlyList<string> SmallNames { get; }
    public IReadOnlyList<string> MediumNames { get; }
    public IReadOnlyList<string> LargeNames { get; }

    public ItemPool(ItemDb db, int playerLevel, IReadOnlyCollection<string>? excludedItems = null)
    {
        var latest = db.GetLatestOnlyNames();
        var small = new List<string>();
        var medium = new List<string>();
        var large = new List<string>();
        var excluded = excludedItems != null
            ? new HashSet<string>(excludedItems, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in latest)
        {
            if (excluded.Contains(name))
                continue;
            var t = db.GetTemplate(name);
            if (t == null || t.Hero != Hero.Vanessa)
                continue;
            if (!GreedyLevelRules.IsMinTierAllowedInPool(t.MinTier, playerLevel))
                continue;

            switch (t.Size)
            {
                case ItemSize.Small: small.Add(name); break;
                case ItemSize.Medium: medium.Add(name); break;
                case ItemSize.Large: large.Add(name); break;
            }
        }

        // 特殊：为烙刀生成 Q1/Q2 两个候选名（由 Greedy resolver 注入 Custom_1）。
        if (!excluded.Contains("烙刀") && medium.Contains("烙刀", StringComparer.Ordinal))
        {
            medium.Add("烙刀（Q1）");
            medium.Add("烙刀（Q2）");
        }

        SmallNames = small;
        MediumNames = medium;
        LargeNames = large;
    }

    public IReadOnlyList<string> NamesForSize(int size) => size switch
    {
        1 => SmallNames,
        2 => MediumNames,
        3 => LargeNames,
        _ => [],
    };

    public int SizeOfItem(string itemName, IItemTemplateResolver db)
    {
        var t = db.GetTemplate(itemName);
        if (t == null) return 0;
        return t.Size switch
        {
            ItemSize.Small => 1,
            ItemSize.Medium => 2,
            ItemSize.Large => 3,
            _ => 0,
        };
    }
}
