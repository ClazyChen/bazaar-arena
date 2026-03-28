using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.ItemDatabase;
using ItemDb = BazaarArena.ItemDatabase.ItemDatabase;

namespace BazaarArena.GreedyDeckFinder;

/// <summary>
/// Greedy 专用 resolver：启动时对物品池模板做一次性扁平化（Bronze 单值化），并按玩家等级预应用 overridable
/// （2 级铜档一半、3 级铜档满值、4 级铜档与银档的平均值，向下取整）。
/// 5 级：扁平化为银档，并对 overridable 应用银档默认值。
/// 6 级：扁平化为银档，并对 overridable 应用银档与金档默认值的平均值（向下取整）。
/// 同时为池内每件物品构造战斗 <see cref="ItemState"/> 原型；<see cref="BattleSimulator"/> 构建卡组时可只拷贝属性数组，避免对同模板重复 GetInt。
/// </summary>
internal sealed class GreedyPreflattenedResolver : IItemBattlePrototypeResolver
{
    private readonly ItemDb _baseDb;
    private readonly int _playerLevel;
    private readonly Dictionary<string, ItemTemplate> _flat = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ItemState> _battleProtoByName = new(StringComparer.Ordinal);

    private static readonly Dictionary<string, (string baseName, int? custom1)> AliasMap = new(StringComparer.Ordinal)
    {
        ["烙刀（Q1）"] = ("烙刀", 1),
        ["烙刀（Q2）"] = ("烙刀", 2),
    };

    /// <param name="playerLevel">2：overridable 铜档一半；3：铜档满值；4：铜档与银档平均；5：银档默认；6：银档与金档平均。</param>
    public GreedyPreflattenedResolver(ItemDb baseDb, ItemPool pool, int playerLevel)
    {
        _baseDb = baseDb;
        _playerLevel = playerLevel;
        var names = pool.SmallNames.Concat(pool.MediumNames).Concat(pool.LargeNames).Distinct(StringComparer.Ordinal).ToList();
        foreach (var name in names)
        {
            var (baseName, custom1) = ResolveAlias(name);
            var t = baseDb.GetTemplate(baseName);
            if (t == null) continue;
            var flat = FlattenWithOverrides(t, playerLevel);
            _flat[name] = flat;
            _battleProtoByName[name] = BuildBattlePrototype(flat, playerLevel, custom1);
        }
    }

    public ItemTemplate? GetTemplate(string name)
    {
        if (_flat.TryGetValue(name, out var t)) return t;
        var (baseName, _) = ResolveAlias(name);
        return _baseDb.GetTemplate(baseName);
    }

    /// <inheritdoc />
    public ItemState? TryGetBattlePrototype(string name) =>
        _battleProtoByName.TryGetValue(name, out var p) ? p : null;

    private static (string baseName, int? custom1) ResolveAlias(string name)
    {
        return AliasMap.TryGetValue(name, out var m) ? m : (name, null);
    }

    private static ItemState BuildBattlePrototype(ItemTemplate flat, int playerLevel, int? custom1)
    {
        var tier = playerLevel >= 5 ? ItemTier.Silver : ItemTier.Bronze;
        var p = new ItemState(flat, tier);
        if (custom1.HasValue)
            p.SetAttribute(Key.Custom_1, custom1.Value);
        return p;
    }

    private static ItemTemplate FlattenWithOverrides(ItemTemplate source, int playerLevel)
    {
        var baseTier = playerLevel >= 5 ? ItemTier.Silver : ItemTier.Bronze;
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
            int v = source.GetInt(key, baseTier);
            flat.SetIntByKey(key, v);
        }

        if (source.OverridableAttributes != null)
        {
            foreach (var kv in source.OverridableAttributes)
            {
                int applied;
                if (playerLevel == 5)
                {
                    applied = source.GetInt(kv.Key, ItemTier.Silver);
                }
                else if (playerLevel == 6)
                {
                    int silverVal = source.GetInt(kv.Key, ItemTier.Silver);
                    int goldVal = source.GetInt(kv.Key, ItemTier.Gold);
                    applied = (silverVal + goldVal) / 2;
                }
                else
                {
                    int bronzeVal = source.GetInt(kv.Key, ItemTier.Bronze);
                    applied = playerLevel switch
                    {
                        2 => bronzeVal / 2,
                        3 => bronzeVal,
                        4 => (bronzeVal + source.GetInt(kv.Key, ItemTier.Silver)) / 2,
                        _ => bronzeVal / 2,
                    };
                }
                flat.SetIntByKey(kv.Key, applied);
            }
        }

        return flat;
    }
}
