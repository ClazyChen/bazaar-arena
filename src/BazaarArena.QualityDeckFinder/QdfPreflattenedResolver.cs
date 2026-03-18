using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using ItemDb = BazaarArena.ItemDatabase.ItemDatabase;

namespace BazaarArena.QualityDeckFinder;

/// <summary>
/// QDF 专用 resolver：启动时对“物品池范围内”的模板做一次性扁平化（Bronze 单值化），并对可复写属性应用「Bronze 默认值的一半」。
/// 目的：避免每局在 BuildSide 中重复做 tier 映射与 overrides 构造。
/// </summary>
internal sealed class QdfPreflattenedResolver : IItemTemplateResolver
{
    private readonly ItemDb _baseDb;
    private readonly Dictionary<string, ItemTemplate> _flat = new(StringComparer.Ordinal);

    public QdfPreflattenedResolver(ItemDb baseDb, ItemPool pool)
    {
        _baseDb = baseDb;
        var names = pool.SmallNames.Concat(pool.MediumNames).Concat(pool.LargeNames).Distinct(StringComparer.Ordinal).ToList();
        foreach (var name in names)
        {
            var t = baseDb.GetTemplate(name);
            if (t == null) continue;
            _flat[name] = FlattenBronzeWithHalfOverrides(t);
        }
    }

    public ItemTemplate? GetTemplate(string name)
    {
        if (_flat.TryGetValue(name, out var t)) return t;
        return _baseDb.GetTemplate(name);
    }

    private static ItemTemplate FlattenBronzeWithHalfOverrides(ItemTemplate source)
    {
        var flat = new ItemTemplate
        {
            Name = source.Name,
            Desc = source.Desc,
            MinTier = source.MinTier,
            Size = source.Size,
            Hero = source.Hero,
            Tags = source.Tags,
            Abilities = source.Abilities,
            Auras = source.Auras,
            // QDF 运行期不需要 OverridableAttributes：避免 DeckRep.ToDeck 再构造 overrides 字典。
            OverridableAttributes = null,
            UpstreamRequirements = source.UpstreamRequirements,
            DownstreamRequirements = source.DownstreamRequirements,
            NeighborPreference = source.NeighborPreference,
        };

        // 扁平化：以 Bronze 为基准读出“该 key 在本模式下的单值”，写回为单值 list。
        // 这里用 Snapshot 获取键集合（会拷贝一次），但仅发生在启动期预处理，不影响每局性能。
        foreach (var kv in source.GetIntsByTierSnapshot())
        {
            string key = kv.Key;
            int v = source.GetInt(key, ItemTier.Bronze, defaultValue: 0);
            flat.SetInt(key, v);
        }

        // 对可复写属性应用「Bronze 默认值的一半」：仅覆盖声明在 OverridableAttributes 内的 key。
        if (source.OverridableAttributes != null)
        {
            foreach (var kv in source.OverridableAttributes)
            {
                int bronzeVal = source.GetInt(kv.Key, ItemTier.Bronze, 0);
                flat.SetInt(kv.Key, bronzeVal / 2);
            }
        }

        return flat;
    }
}

