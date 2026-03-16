using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using ItemDb = BazaarArena.ItemDatabase.ItemDatabase;

namespace BazaarArena.QualityDeckFinder;

/// <summary>海盗（Vanessa）最新版、铜级物品池，按尺寸分组。</summary>
public class ItemPool
{
    public IReadOnlyList<string> SmallNames { get; }
    public IReadOnlyList<string> MediumNames { get; }
    public IReadOnlyList<string> LargeNames { get; }

    public ItemPool(ItemDb db)
    {
        var latest = db.GetLatestOnlyNames();
        var small = new List<string>();
        var medium = new List<string>();
        var large = new List<string>();
        foreach (var name in latest)
        {
            var t = db.GetTemplate(name);
            if (t == null || (t.Hero ?? Hero.Common) != Hero.Vanessa || t.MinTier != ItemTier.Bronze)
                continue;
            switch (t.Size)
            {
                case ItemSize.Small: small.Add(name); break;
                case ItemSize.Medium: medium.Add(name); break;
                case ItemSize.Large: large.Add(name); break;
            }
        }
        SmallNames = small;
        MediumNames = medium;
        LargeNames = large;
    }

    /// <summary>根据槽位尺寸（1/2/3）获取对应物品名列表。</summary>
    public IReadOnlyList<string> NamesForSize(int size) => size switch
    {
        1 => SmallNames,
        2 => MediumNames,
        3 => LargeNames,
        _ => [],
    };

    /// <summary>某形状是否可生成无重复卡组（每尺寸槽位数不超过该尺寸池大小）。</summary>
    public bool CanBuildNoDuplicate(IReadOnlyList<int> shape)
    {
        var needSmall = shape.Count(x => x == 1);
        var needMedium = shape.Count(x => x == 2);
        var needLarge = shape.Count(x => x == 3);
        return needSmall <= SmallNames.Count && needMedium <= MediumNames.Count && needLarge <= LargeNames.Count;
    }
}
