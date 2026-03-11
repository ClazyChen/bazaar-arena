using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;

namespace BazaarArena.Core;

/// <summary>秒数：单值或按等级。用于物品定义中时间类属性（如 FreezeSeconds），保证以秒为单位可读。</summary>
public readonly struct SecondsOrByTier
{
    private readonly double[]? _values;

    private SecondsOrByTier(double[] values) => _values = values;

    internal static SecondsOrByTier FromFirstTierMs(int ms) => new([ms / 1000.0]);

    public static implicit operator SecondsOrByTier(double single) => new([single]);
    public static implicit operator SecondsOrByTier(double[] byTier) => new(byTier);

    /// <summary>将秒转换为毫秒列表（统一语义，供 Freeze/Slow/Haste 等时间属性写入模板）。</summary>
    internal List<int> ToMilliseconds() => _values?.Select(s => (int)(s * 1000)).ToList() ?? [];

    public static implicit operator double(SecondsOrByTier s) => s._values?.Length > 0 ? s._values[0] : 0;
}

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

    /// <summary>每个 tier 的值都加上 delta，返回新的 IntOrByTier（战斗内临时修改用，如举重手套加武器伤害）。</summary>
    public readonly IntOrByTier Add(int delta)
    {
        var list = ToList();
        if (list.Count == 0) return new IntOrByTier([delta, delta, delta, delta]);
        return new IntOrByTier(list.Select(v => v + delta).ToList());
    }

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
    private const string KeyCritDamagePercent = "CritDamagePercent";
    private const string KeyMulticast = "Multicast";
    private const string KeyAmmoCap = "AmmoCap";
    private const string KeyDamage = "Damage";
    private const string KeyBurn = "Burn";
    private const string KeyPoison = "Poison";
    private const string KeyHeal = "Heal";
    private const string KeyShield = "Shield";
    private const string KeyCharge = "Charge";
    private const string KeyFreeze = "Freeze";
    private const string KeyFreezeTargetCount = "FreezeTargetCount";
    private const string KeySlow = "Slow";
    private const string KeySlowTargetCount = "SlowTargetCount";
    private const string KeyHaste = "Haste";
    private const string KeyHasteTargetCount = "HasteTargetCount";
    private const string KeyRepairTargetCount = "RepairTargetCount";
    private const string KeyLifeSteal = "LifeSteal";
    private const string KeyCustom_0 = "Custom_0";
    private const string KeyStashParameter = "StashParameter";

    /// <summary>根据字段名读取 int 值（无 tier 时按第一档），不存在则返回 0。</summary>
    public int GetInt(string key) => GetInt(key, ItemTier.Bronze, 0);

    /// <summary>根据字段名读取 int 值（无 tier 时按第一档），不存在则返回指定默认值。</summary>
    public int GetInt(string key, int defaultValue) => GetInt(key, ItemTier.Bronze, defaultValue);

    /// <summary>根据字段名与当前等级读取：若列表长度为 1 则返回该值，否则按 tier 取；列表仅含 MinTier 起的各档（如最小银则 list[0]=银、list[1]=金、list[2]=钻），按偏移映射。不存在则返回默认值。</summary>
    public int GetInt(string key, ItemTier tier, int defaultValue = 0)
    {
        if (!_intsByTier.TryGetValue(key, out var list) || list.Count == 0)
            return defaultValue;
        if (list.Count == 1)
            return list[0];
        int minIdx = (int)MinTier;
        int listIndex = (int)tier - minIdx;
        if (listIndex < 0)
            return list[0];
        if (listIndex >= list.Count)
            return list[list.Count - 1];
        return list[listIndex];
    }

    /// <summary>根据字段名与等级读取，若有光环上下文则先取基础值再叠加光环：最终值 = (基础 + Σ固定) × (1 + Σ百分比/100)。</summary>
    public int GetInt(string key, ItemTier tier, int defaultValue, IAuraContext? context)
    {
        int baseVal = GetInt(key, tier, defaultValue);
        if (context == null)
            return baseVal;
        context.GetAuraModifiers(key, out int fixedSum, out int percentSum);
        return (int)Math.Round((baseVal + fixedSum) * (1 + percentSum / 100.0));
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

    /// <summary>按 key 读取完整按等级列表（用于可被战斗内修改的字段，如 Damage）。</summary>
    private IntOrByTier GetIntOrByTier(string key)
    {
        if (!_intsByTier.TryGetValue(key, out var list) || list.Count == 0) return default;
        return (IntOrByTier)new List<int>(list);
    }

    /// <summary>冷却时间（毫秒）。设计文档：最低只能减到 1 秒。</summary>
    public IntOrByTier CooldownMs { get => GetInt(KeyCooldownMs); set => SetIntOrByTier(KeyCooldownMs, value.ToList()); }

    /// <summary>冷却时间（秒）。设置时转换为 CooldownMs（仅支持单值）；不指定时可省略。</summary>
    public double Cooldown { get => GetInt(KeyCooldownMs) / 1000.0; set => SetInt(KeyCooldownMs, (int)(value * 1000)); }

    /// <summary>暴击率（百分比，0–100）。默认 0，不指定时可省略。</summary>
    public IntOrByTier CritRatePercent { get => GetInt(KeyCritRatePercent, 0); set => SetIntOrByTier(KeyCritRatePercent, value.ToList()); }

    /// <summary>暴击伤害（百分比，200 表示 2 倍暴击）。默认 200，作用于伤害、灼烧、剧毒、治疗等可暴击效果。</summary>
    public IntOrByTier CritDamagePercent { get => GetInt(KeyCritDamagePercent, 200); set => SetIntOrByTier(KeyCritDamagePercent, value.ToList()); }

    /// <summary>多重触发。默认 1，不指定时可省略。</summary>
    public IntOrByTier Multicast { get => GetInt(KeyMulticast, 1); set => SetIntOrByTier(KeyMulticast, value.ToList()); }

    /// <summary>弹药上限，0 表示不依赖弹药。默认 0，不指定时可省略。</summary>
    public IntOrByTier AmmoCap { get => GetInt(KeyAmmoCap, 0); set => SetIntOrByTier(KeyAmmoCap, value.ToList()); }

    /// <summary>伤害值（可单值或按等级）；get 返回完整按等级列表，便于战斗内做 .Add(delta) 等修改。</summary>
    public IntOrByTier Damage { get => GetIntOrByTier(KeyDamage); set => SetIntOrByTier(KeyDamage, value.ToList()); }

    /// <summary>灼烧值（可单值或按等级）。</summary>
    public IntOrByTier Burn { get => GetInt(KeyBurn, 0); set => SetIntOrByTier(KeyBurn, value.ToList()); }

    /// <summary>剧毒值（可单值或按等级）。</summary>
    public IntOrByTier Poison { get => GetInt(KeyPoison, 0); set => SetIntOrByTier(KeyPoison, value.ToList()); }

    /// <summary>治疗值（可单值或按等级）。</summary>
    public IntOrByTier Heal { get => GetInt(KeyHeal, 0); set => SetIntOrByTier(KeyHeal, value.ToList()); }

    /// <summary>护盾值（可单值或按等级）。</summary>
    public IntOrByTier Shield { get => GetInt(KeyShield, 0); set => SetIntOrByTier(KeyShield, value.ToList()); }

    /// <summary>充能值（毫秒，可单值或按等级）；用于 ChargeSelf 效果为此物品增加已过冷却时间。</summary>
    public IntOrByTier Charge { get => GetInt(KeyCharge, 0); set => SetIntOrByTier(KeyCharge, value.ToList()); }

    /// <summary>充能时间（秒）。设置时转换为 Charge 毫秒（仅支持单值）；定义物品时可用此属性以秒书写。</summary>
    public double ChargeSeconds { get => GetInt(KeyCharge, 0) / 1000.0; set => SetInt(KeyCharge, (int)(value * 1000)); }

    /// <summary>冻结时长（毫秒，可单值或按等级）；用于冻结效果。内部存储用，定义物品时请用 FreezeSeconds（秒）以保证可读性。</summary>
    public IntOrByTier Freeze { get => GetIntOrByTier(KeyFreeze); set => SetIntOrByTier(KeyFreeze, value.ToList()); }

    /// <summary>冻结时长（秒）。可赋单值或按等级 [3.0, 4.0, 5.0, 6.0]，内部转换为毫秒存储。物品定义中时间一律用秒。</summary>
    public SecondsOrByTier FreezeSeconds { get => SecondsOrByTier.FromFirstTierMs(GetInt(KeyFreeze, 0)); set => SetIntOrByTier(KeyFreeze, value.ToMilliseconds()); }

    /// <summary>冻结目标数量（可单值或按等级）；随机选取敌人物品时选取的次数（每次独立，可能重复）。</summary>
    public IntOrByTier FreezeTargetCount { get => GetInt(KeyFreezeTargetCount, 1); set => SetIntOrByTier(KeyFreezeTargetCount, value.ToList()); }

    /// <summary>减速时长（毫秒，可单值或按等级）；用于减速效果。内部存储用，定义物品时请用 SlowSeconds（秒）。</summary>
    public IntOrByTier Slow { get => GetIntOrByTier(KeySlow); set => SetIntOrByTier(KeySlow, value.ToList()); }

    /// <summary>减速时长（秒）。可赋单值或按等级，内部转换为毫秒存储。物品定义中时间一律用秒。</summary>
    public SecondsOrByTier SlowSeconds { get => SecondsOrByTier.FromFirstTierMs(GetInt(KeySlow, 0)); set => SetIntOrByTier(KeySlow, value.ToMilliseconds()); }

    /// <summary>减速目标数量（可单值或按等级）；随机选取敌人物品时选取的次数（每次独立，可能重复）。</summary>
    public IntOrByTier SlowTargetCount { get => GetInt(KeySlowTargetCount, 1); set => SetIntOrByTier(KeySlowTargetCount, value.ToList()); }

    /// <summary>加速时长（毫秒，可单值或按等级）；用于加速效果。内部存储用，定义物品时请用 HasteSeconds（秒）。</summary>
    public IntOrByTier Haste { get => GetIntOrByTier(KeyHaste); set => SetIntOrByTier(KeyHaste, value.ToList()); }

    /// <summary>加速时长（秒）。可赋单值或按等级，内部转换为毫秒存储。物品定义中时间一律用秒。</summary>
    public SecondsOrByTier HasteSeconds { get => SecondsOrByTier.FromFirstTierMs(GetInt(KeyHaste, 0)); set => SetIntOrByTier(KeyHaste, value.ToMilliseconds()); }

    /// <summary>加速目标数量（可单值或按等级）；与 TargetCondition 配合，从己方有冷却物品中选取。</summary>
    public IntOrByTier HasteTargetCount { get => GetInt(KeyHasteTargetCount, 1); set => SetIntOrByTier(KeyHasteTargetCount, value.ToList()); }

    /// <summary>修复目标数量（可单值或按等级）；与 TargetCondition 配合，从己方已摧毁物品中选取。</summary>
    public IntOrByTier RepairTargetCount { get => GetInt(KeyRepairTargetCount, 1); set => SetIntOrByTier(KeyRepairTargetCount, value.ToList()); }

    /// <summary>吸血：1 表示造成伤害时按实际伤害量治疗己方，0 表示无。用于伤害效果。</summary>
    public IntOrByTier LifeSteal { get => GetInt(KeyLifeSteal, 0); set => SetIntOrByTier(KeyLifeSteal, value.ToList()); }

    /// <summary>自定义变量 0（可单值或按等级），用于如举重手套的武器伤害提升量等。</summary>
    public IntOrByTier Custom_0 { get => GetInt(KeyCustom_0, 0); set => SetIntOrByTier(KeyCustom_0, value.ToList()); }

    /// <summary>储存箱等效参数（可单值或按等级），用于如废品场长枪的「卡组小型物品数 + StashParameter」公式。默认 0。</summary>
    public IntOrByTier StashParameter { get => GetInt(KeyStashParameter, 0); set => SetIntOrByTier(KeyStashParameter, value.ToList()); }

    /// <summary>可被卡组复写的属性及按 tier 的默认值（nameof(属性) → IntOrByTier）；null 表示无。拖入卡组或改 tier 时用此初始化/更新 Overrides。</summary>
    public Dictionary<string, IntOrByTier>? OverridableAttributes { get; set; }

    public List<AbilityDefinition> Abilities { get; set; } = [];
    /// <summary>光环列表：当在战斗内读取属性并传入 IAuraContext 时，会按条件与公式叠加这些光环。</summary>
    public List<AuraDefinition> Auras { get; set; } = [];

    /// <summary>获取按等级扩展属性的只读副本，用于序列化或复制。</summary>
    public IReadOnlyDictionary<string, List<int>> GetIntsByTierSnapshot() =>
        _intsByTier.ToDictionary(kv => kv.Key, kv => new List<int>(kv.Value));
}
