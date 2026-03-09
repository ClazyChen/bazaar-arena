using System.Collections;
using System.Runtime.CompilerServices;

namespace BazaarArena.Core;

/// <summary>单值或按等级列表，用于对象初始器中支持 Damage = 40 与 Damage = [25, 35, 45, 55] 两种写法。</summary>
[CollectionBuilder(typeof(IntOrByTier), "Create")]
public readonly struct IntOrByTier : IEnumerable<int>
{
    private readonly List<int> _values;

    private IntOrByTier(List<int> values) => _values = values;

    /// <summary>供集合表达式 [a, b, c] 使用的工厂方法。</summary>
    public static IntOrByTier Create(ReadOnlySpan<int> values)
    {
        var list = new List<int>(values.Length);
        for (int i = 0; i < values.Length; i++)
            list.Add(values[i]);
        return new IntOrByTier(list);
    }

    public static implicit operator IntOrByTier(int single) =>
        new([single]);

    public static implicit operator IntOrByTier(int[] byTier) =>
        new([..byTier]);

    public static implicit operator IntOrByTier(List<int> byTier) =>
        new([..byTier]);

    public List<int> ToList() => _values ?? [];

    public List<int>.Enumerator GetEnumerator() => (_values ?? []).GetEnumerator();
    IEnumerator<int> IEnumerable<int>.GetEnumerator() => (_values ?? []).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => (_values ?? []).GetEnumerator();
}

/// <summary>物品模板：名称、最低等级、尺寸、标签、属性、能力与光环。由物品数据库按名称创建实例。</summary>
public class ItemTemplate
{
    public string Name { get; set; } = "";
    /// <summary>描述文本，可含占位符如 {Damage}、{Cooldown}，用于悬停说明等。</summary>
    public string Desc { get; set; } = "";
    public ItemTier MinTier { get; set; }
    public ItemSize Size { get; set; }
    public List<string> Tags { get; set; } = [];

    /// <summary>按等级区分的扩展属性：键为字段名，值为按 ItemTier 顺序（Bronze=0, Silver=1, Gold=2, Diamond=3）的列表；单值存为长度为 1 的列表。</summary>
    private Dictionary<string, List<int>> _intsByTier = [];

    private const string KeyCooldownMs = "CooldownMs";
    private const string KeyCritRatePercent = "CritRatePercent";
    private const string KeyMulticast = "Multicast";
    private const string KeyAmmoCap = "AmmoCap";
    private const string KeyDamage = "Damage";

    /// <summary>根据字段名读取 int 值（无 tier 时按第一档），不存在则返回 0。</summary>
    public int GetInt(string key) => GetInt(key, ItemTier.Bronze, 0);

    /// <summary>根据字段名读取 int 值（无 tier 时按第一档），不存在则返回指定默认值。</summary>
    public int GetInt(string key, int defaultValue) => GetInt(key, ItemTier.Bronze, defaultValue);

    /// <summary>根据字段名与当前等级读取：若列表长度为 1 则返回该值，否则按 tier 下标取；不存在则返回默认值。</summary>
    public int GetInt(string key, ItemTier tier, int defaultValue = 0)
    {
        if (!_intsByTier.TryGetValue(key, out var list) || list.Count == 0)
            return defaultValue;
        if (list.Count == 1)
            return list[0];
        int ti = (int)tier;
        return ti >= 0 && ti < list.Count ? list[ti] : defaultValue;
    }

    /// <summary>写入单值（用于 Overrides 等），存为长度为 1 的列表。</summary>
    public void SetInt(string key, int value) => _intsByTier[key] = [value];

    private void SetIntOrByTier(string key, IEnumerable<int> values) =>
        _intsByTier[key] = values.ToList();

    /// <summary>按等级设置字段值，用于克隆/序列化。</summary>
    public void SetIntsByTier(IEnumerable<KeyValuePair<string, List<int>>> pairs)
    {
        foreach (var kv in pairs)
            _intsByTier[kv.Key] = new List<int>(kv.Value);
    }

    /// <summary>冷却时间（毫秒）。设计文档：最低只能减到 1 秒。</summary>
    public IntOrByTier CooldownMs { get => GetInt(KeyCooldownMs); set => SetIntOrByTier(KeyCooldownMs, value.ToList()); }

    /// <summary>冷却时间（秒）。设置时转换为 CooldownMs（仅支持单值）；不指定时可省略。</summary>
    public double Cooldown { get => GetInt(KeyCooldownMs) / 1000.0; set => SetInt(KeyCooldownMs, (int)(value * 1000)); }

    /// <summary>暴击率（百分比，0–100）。默认 0，不指定时可省略。</summary>
    public IntOrByTier CritRatePercent { get => GetInt(KeyCritRatePercent, 0); set => SetIntOrByTier(KeyCritRatePercent, value.ToList()); }

    /// <summary>多重触发。默认 1，不指定时可省略。</summary>
    public IntOrByTier Multicast { get => GetInt(KeyMulticast, 1); set => SetIntOrByTier(KeyMulticast, value.ToList()); }

    /// <summary>弹药上限，0 表示不依赖弹药。默认 0，不指定时可省略。</summary>
    public IntOrByTier AmmoCap { get => GetInt(KeyAmmoCap, 0); set => SetIntOrByTier(KeyAmmoCap, value.ToList()); }

    /// <summary>伤害值（可单值或按等级）。</summary>
    public IntOrByTier Damage { get => GetInt(KeyDamage); set => SetIntOrByTier(KeyDamage, value.ToList()); }

    public List<AbilityDefinition> Abilities { get; set; } = [];
    /// <summary>光环列表（基座阶段可留空，后续按属性、条件、百分比加算实现）。</summary>
    public List<object> Auras { get; set; } = [];

    /// <summary>获取按等级扩展属性的只读副本，用于序列化或复制。</summary>
    public IReadOnlyDictionary<string, List<int>> GetIntsByTierSnapshot() =>
        _intsByTier.ToDictionary(kv => kv.Key, kv => new List<int>(kv.Value));
}
