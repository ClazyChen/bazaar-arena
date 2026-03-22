using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using ItemDb = BazaarArena.ItemDatabase.ItemDatabase;

namespace BazaarArena.GreedyDeckFinder;

/// <summary>
/// Greedy 专用 resolver：启动时对物品池模板做一次性扁平化（Bronze 单值化），并按玩家等级预应用 overridable
/// （2 级铜档一半、3 级铜档满值、4 级铜档与银档的平均值，向下取整）。
/// 目的是避免每局重复 tier 映射与 overrides 构造。
/// </summary>
internal sealed class GreedyPreflattenedResolver : IItemTemplateResolver
{
    private readonly ItemDb _baseDb;
    private readonly Dictionary<string, ItemTemplate> _flat = new(StringComparer.Ordinal);

    /// <param name="playerLevel">2：overridable 铜档一半；3：铜档满值；4：铜档与银档平均。</param>
    public GreedyPreflattenedResolver(ItemDb baseDb, ItemPool pool, int playerLevel)
    {
        _baseDb = baseDb;
        var names = pool.SmallNames.Concat(pool.MediumNames).Concat(pool.LargeNames).Distinct(StringComparer.Ordinal).ToList();
        foreach (var name in names)
        {
            var t = baseDb.GetTemplate(name);
            if (t == null) continue;
            _flat[name] = FlattenBronzeWithOverrides(t, playerLevel);
        }
    }

    public ItemTemplate? GetTemplate(string name)
    {
        if (_flat.TryGetValue(name, out var t)) return t;
        return _baseDb.GetTemplate(name);
    }

    private static ItemTemplate FlattenBronzeWithOverrides(ItemTemplate source, int playerLevel)
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
            OverridableAttributes = null,
        };

        foreach (var kv in source.GetIntsByTierSnapshot())
        {
            string key = kv.Key;
            int v = source.GetInt(key, ItemTier.Bronze, defaultValue: 0);
            flat.SetInt(key, v);
        }

        if (source.OverridableAttributes != null)
        {
            foreach (var kv in source.OverridableAttributes)
            {
                int bronzeVal = source.GetInt(kv.Key, ItemTier.Bronze, 0);
                int applied = playerLevel switch
                {
                    2 => bronzeVal / 2,
                    3 => bronzeVal,
                    4 => (bronzeVal + source.GetInt(kv.Key, ItemTier.Silver, bronzeVal)) / 2,
                    _ => bronzeVal / 2,
                };
                flat.SetInt(kv.Key, applied);
            }
        }

        return flat;
    }
}
