using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using ItemDb = BazaarArena.ItemDatabase.ItemDatabase;

namespace BazaarArena.GreedyDeckFinder;

/// <summary>
/// Greedy 专用 resolver：启动时对物品池模板做一次性扁平化（Bronze 单值化），并按玩家等级预应用 overridable
/// （2 级铜档一半、3 级铜档满值、4 级铜档与银档的平均值，向下取整）。
/// 同时为池内每件物品构造铜档 <see cref="ItemState"/> 原型；<see cref="BattleSimulator"/> 构建卡组时可只拷贝属性数组，避免对同模板重复 GetInt。
/// </summary>
internal sealed class GreedyPreflattenedResolver : IItemBattlePrototypeResolver
{
    private readonly ItemDb _baseDb;
    private readonly Dictionary<string, ItemTemplate> _flat = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ItemState> _battleProtoByName = new(StringComparer.Ordinal);

    /// <param name="playerLevel">2：overridable 铜档一半；3：铜档满值；4：铜档与银档平均。</param>
    public GreedyPreflattenedResolver(ItemDb baseDb, ItemPool pool, int playerLevel)
    {
        _baseDb = baseDb;
        var names = pool.SmallNames.Concat(pool.MediumNames).Concat(pool.LargeNames).Distinct(StringComparer.Ordinal).ToList();
        foreach (var name in names)
        {
            var t = baseDb.GetTemplate(name);
            if (t == null) continue;
            var flat = FlattenBronzeWithOverrides(t, playerLevel);
            _flat[name] = flat;
            _battleProtoByName[name] = new ItemState(flat, ItemTier.Bronze);
        }
    }

    public ItemTemplate? GetTemplate(string name)
    {
        if (_flat.TryGetValue(name, out var t)) return t;
        return _baseDb.GetTemplate(name);
    }

    /// <inheritdoc />
    public ItemState? TryGetBattlePrototype(string name) =>
        _battleProtoByName.TryGetValue(name, out var p) ? p : null;

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
            int key = kv.Key;
            int v = source.GetInt(key, ItemTier.Bronze);
            flat.SetIntByKey(key, v);
        }

        if (source.OverridableAttributes != null)
        {
            foreach (var kv in source.OverridableAttributes)
            {
                int bronzeVal = source.GetInt(kv.Key, ItemTier.Bronze);
                int applied = playerLevel switch
                {
                    2 => bronzeVal / 2,
                    3 => bronzeVal,
                    4 => (bronzeVal + source.GetInt(kv.Key, ItemTier.Silver)) / 2,
                    _ => bronzeVal / 2,
                };
                flat.SetIntByKey(kv.Key, applied);
            }
        }

        return flat;
    }
}
