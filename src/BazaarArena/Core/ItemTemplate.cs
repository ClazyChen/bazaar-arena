using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;

namespace BazaarArena.Core;

/// <summary>秒数：单值或按等级。用于物品定义中时间类属性（如 Cooldown、Charge、Freeze），保证以秒为单位可读；支持 Cooldown = [9.0, 8.0, 7.0] 集合表达式。</summary>
[CollectionBuilder(typeof(SecondsOrByTier), "Create")]
public readonly struct SecondsOrByTier : IEnumerable<double>
{
    private readonly double[]? _values;

    private SecondsOrByTier(double[] values) => _values = values;

    /// <summary>供集合表达式 [a, b, c] 使用的工厂方法。</summary>
    public static SecondsOrByTier Create(ReadOnlySpan<double> values)
    {
        var arr = new double[values.Length];
        for (int i = 0; i < values.Length; i++)
            arr[i] = values[i];
        return new SecondsOrByTier(arr);
    }

    internal static SecondsOrByTier FromFirstTierMs(int ms) => new([ms / 1000.0]);

    /// <summary>从毫秒列表转换为秒（按档位）；用于模板 getter。</summary>
    internal static SecondsOrByTier FromMilliseconds(IReadOnlyList<int>? ms)
    {
        if (ms == null || ms.Count == 0) return default;
        var arr = new double[ms.Count];
        for (int i = 0; i < ms.Count; i++) arr[i] = ms[i] / 1000.0;
        return new SecondsOrByTier(arr);
    }

    public static implicit operator SecondsOrByTier(double single) => new([single]);
    public static implicit operator SecondsOrByTier(double[] byTier) => new(byTier);

    /// <summary>将秒转换为毫秒列表（统一语义，供 Freeze/Slow/Haste 等时间属性写入模板）。</summary>
    internal List<int> ToMilliseconds() => _values?.Select(s => (int)(s * 1000)).ToList() ?? [];

    public static implicit operator double(SecondsOrByTier s) => s._values?.Length > 0 ? s._values[0] : 0;

    public IEnumerator<double> GetEnumerator() => (_values ?? []).AsEnumerable().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
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

    /// <summary>单值隐式转换：显式构造含一元素的列表，保证 ToList() 非空，避免 ChargeTargetCount = 10 等写入空列表导致读回默认值 1。</summary>
    public static implicit operator IntOrByTier(int single) =>
        new IntOrByTier(new List<int> { single });

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
        return [.. list.Select(v => v + delta).ToList()];
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
    /// <summary>所属英雄标识，如 Hero.Common；默认 Common，后续可扩展其他英雄。</summary>
    public string Hero { get; set; } = global::BazaarArena.Core.Hero.Common;
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
    private const string KeyGold = "Gold";
    private const string KeyCharge = "Charge";
    private const string KeyChargeTargetCount = "ChargeTargetCount";
    private const string KeyFreeze = "Freeze";
    private const string KeyFreezeTargetCount = "FreezeTargetCount";
    private const string KeyPercentFreezeReduction = "PercentFreezeReduction";
    private const string KeySlow = "Slow";
    private const string KeySlowTargetCount = "SlowTargetCount";
    private const string KeyHaste = "Haste";
    private const string KeyHasteTargetCount = "HasteTargetCount";
    private const string KeyRepairTargetCount = "RepairTargetCount";
    private const string KeyDestroyTargetCount = "DestroyTargetCount";
    private const string KeyModifyAttributeTargetCount = "ModifyAttributeTargetCount";
    private const string KeyLifeSteal = "LifeSteal";
    private const string KeyCustom_0 = "Custom_0";
    private const string KeyCustom_1 = "Custom_1";
    private const string KeyCustom_2 = "Custom_2";
    private const string KeyPrice = "Price";
    private const string KeyStashParameter = "StashParameter";

    /// <summary>战斗运行时变量键（由模拟器写入，与按等级属性无名称冲突）；可通过 GetInt/GetBool 一致解析。</summary>
    public const string KeySideIndex = "SideIndex";
    public const string KeyItemIndex = "ItemIndex";
    public const string KeyTier = "Tier";
    public const string KeyCooldownElapsedMs = "CooldownElapsedMs";
    public const string KeyHasteRemainingMs = "HasteRemainingMs";
    public const string KeySlowRemainingMs = "SlowRemainingMs";
    public const string KeyFreezeRemainingMs = "FreezeRemainingMs";
    public const string KeyInFlight = "InFlight";
    public const string KeyDestroyed = "Destroyed";
    public const string KeyAmmoRemaining = "AmmoRemaining";
    /// <summary>每个能力上次触发时间（毫秒）的键前缀，完整键为 KeyLastTriggerMsPrefix + abilityIndex。</summary>
    public const string KeyLastTriggerMsPrefix = "LastTriggerMs_";
    /// <summary>本次暴击判定所适用的时间（毫秒）；与当前帧 timeMs 相同时表示本帧已判定可复用。</summary>
    public const string KeyCritTimeMs = "CritTimeMs";
    /// <summary>本次判定是否暴击（0/1）。</summary>
    public const string KeyIsCritThisUse = "IsCritThisUse";
    /// <summary>本次判定若暴击时的暴击伤害百分比（复用时可避免重复读光环）。</summary>
    public const string KeyCritDamagePercentThisUse = "CritDamagePercentThisUse";

    /// <summary>未定义时该 key 的显示/逻辑默认值（与各属性 getter 一致）；用于 Desc 占位符等。未知 key 返回 0。</summary>
    public static int GetDefaultValueForKey(string key) => key switch
    {
        KeyCritDamagePercent => 200,
        KeyMulticast => 1,
        KeyChargeTargetCount => 1,
        KeyFreezeTargetCount => 1,
        KeySlowTargetCount => 1,
        KeyHasteTargetCount => 1,
        KeyRepairTargetCount => 1,
        KeyDestroyTargetCount => 1,
        KeyModifyAttributeTargetCount => 20,
        _ => 0,
    };

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

    /// <summary>根据字段名与等级读取，若有光环上下文则先取基础值再叠加光环：最终值 = (基础 + Σ固定) × (1 + Σ百分比/100)。冷却时间（CooldownMs）有光环时：先按 PercentCooldownReduction 做百分比缩减，再叠加 CooldownMs 的固定/百分比光环，至少为 1 秒。</summary>
    public int GetInt(string key, ItemTier tier, int defaultValue, IAuraContext? context)
    {
        int baseVal = GetInt(key, tier, defaultValue);
        if (context == null)
            return baseVal;
        if (key == KeyCooldownMs && baseVal > 0)
        {
            context.GetAuraModifiers(Key.PercentCooldownReduction, out int redFix, out int redPct);
            int totalRed = Math.Min(99, redFix + redPct);
            int afterReduction = baseVal * (100 - totalRed) / 100;
            context.GetAuraModifiers(key, out int cdFixed, out int cdPercent);
            int cdResult = (int)Math.Round((afterReduction + cdFixed) * (1 + cdPercent / 100.0));
            return Math.Max(1000, cdResult);
        }
        context.GetAuraModifiers(key, out int fixedSum, out int percentSum);
        int result = (int)Math.Round((baseVal + fixedSum) * (1 + percentSum / 100.0));
        return result;
    }

    /// <summary>写入单值（用于 Overrides、运行时变量等），存为长度为 1 的列表。</summary>
    public void SetInt(string key, int value) => _intsByTier[key] = [value];

    /// <summary>按 key 读取 bool（存为 0/1），不存在或为 0 则返回 false。</summary>
    public bool GetBool(string key) => GetInt(key, 0) != 0;

    /// <summary>按 key 写入 bool（true→1，false→0）。</summary>
    public void SetBool(string key, bool value) => SetInt(key, value ? 1 : 0);

    private void SetIntOrByTier(string key, IEnumerable<int> values) =>
        _intsByTier[key] = values.ToList();

    /// <summary>按 key 设置 IntOrByTier，供 Register 从 OverridableAttributes 同步默认值到模板时使用。</summary>
    public void SetIntOrByTierByKey(string key, IntOrByTier value) => SetIntOrByTier(key, value.ToList());

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

    /// <summary>冷却时间（毫秒）。设计文档：最低只能减到 1 秒。内部存储用，定义时请用 Cooldown（秒）。</summary>
    public IntOrByTier CooldownMs { get => GetInt(KeyCooldownMs); set => SetIntOrByTier(KeyCooldownMs, value.ToList()); }

    /// <summary>冷却时间（秒，可单值或按等级）。Cooldown = 5.0 或 Cooldown = [9.0, 8.0, 7.0]；内部存为 CooldownMs。</summary>
    public SecondsOrByTier Cooldown { get => SecondsOrByTier.FromMilliseconds(GetIntOrByTier(KeyCooldownMs).ToList()); set => SetIntOrByTier(KeyCooldownMs, value.ToMilliseconds()); }

    /// <summary>暴击率（百分比，0–100）。默认 0，不指定时可省略。</summary>
    public IntOrByTier CritRatePercent { get => GetInt(KeyCritRatePercent, 0); set => SetIntOrByTier(KeyCritRatePercent, value.ToList()); }

    /// <summary>暴击伤害（百分比，200 表示 2 倍暴击）。默认 200，作用于伤害、灼烧、剧毒、治疗等可暴击效果。</summary>
    public IntOrByTier CritDamagePercent { get => GetInt(KeyCritDamagePercent, 200); set => SetIntOrByTier(KeyCritDamagePercent, value.ToList()); }

    /// <summary>多重释放。默认 1，不指定时可省略。</summary>
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

    /// <summary>获取金币数量（可单值或按等级）；用于 Ability.GainGold。</summary>
    public IntOrByTier Gold { get => GetInt(KeyGold, 0); set => SetIntOrByTier(KeyGold, value.ToList()); }

    /// <summary>充能时间（秒，可单值或按等级）。Charge = 2.0 或 Charge = [1.0, 2.0]；内部存为毫秒。</summary>
    public SecondsOrByTier Charge { get => SecondsOrByTier.FromMilliseconds(GetIntOrByTier(KeyCharge).ToList()); set => SetIntOrByTier(KeyCharge, value.ToMilliseconds()); }

    /// <summary>充能目标数量（可单值或按等级）；与 TargetCondition 配合，从己方有冷却物品中选取，默认 1。</summary>
    public IntOrByTier ChargeTargetCount { get => GetInt(KeyChargeTargetCount, 1); set => SetIntOrByTier(KeyChargeTargetCount, value.ToList()); }

    /// <summary>冻结时长（秒，可单值或按等级）。Freeze = 3.0 或 Freeze = [3.0, 4.0, 5.0, 6.0]；内部存为毫秒。</summary>
    public SecondsOrByTier Freeze { get => SecondsOrByTier.FromMilliseconds(GetIntOrByTier(KeyFreeze).ToList()); set => SetIntOrByTier(KeyFreeze, value.ToMilliseconds()); }

    /// <summary>冻结目标数量（可单值或按等级）；随机选取敌人物品时选取的次数（每次独立，可能重复）。</summary>
    public IntOrByTier FreezeTargetCount { get => GetInt(KeyFreezeTargetCount, 1); set => SetIntOrByTier(KeyFreezeTargetCount, value.ToList()); }

    /// <summary>冻结时长减免百分比（0–100）；施加冻结时有效时长 = 原始时长 × (100 - 此值) / 100。默认 0；可由光环提供。</summary>
    public IntOrByTier PercentFreezeReduction { get => GetInt(KeyPercentFreezeReduction, 0); set => SetIntOrByTier(KeyPercentFreezeReduction, value.ToList()); }

    /// <summary>减速时长（秒，可单值或按等级）。Slow = 1.0 或 Slow = [1.0, 2.0, 3.0]；内部存为毫秒。</summary>
    public SecondsOrByTier Slow { get => SecondsOrByTier.FromMilliseconds(GetIntOrByTier(KeySlow).ToList()); set => SetIntOrByTier(KeySlow, value.ToMilliseconds()); }

    /// <summary>减速目标数量（可单值或按等级）；随机选取敌人物品时选取的次数（每次独立，可能重复）。</summary>
    public IntOrByTier SlowTargetCount { get => GetInt(KeySlowTargetCount, 1); set => SetIntOrByTier(KeySlowTargetCount, value.ToList()); }

    /// <summary>加速时长（秒，可单值或按等级）。Haste = 1.0 或 Haste = [1.0, 2.0, 3.0]；内部存为毫秒。</summary>
    public SecondsOrByTier Haste { get => SecondsOrByTier.FromMilliseconds(GetIntOrByTier(KeyHaste).ToList()); set => SetIntOrByTier(KeyHaste, value.ToMilliseconds()); }

    /// <summary>加速目标数量（可单值或按等级）；与 TargetCondition 配合，从己方有冷却物品中选取。</summary>
    public IntOrByTier HasteTargetCount { get => GetInt(KeyHasteTargetCount, 1); set => SetIntOrByTier(KeyHasteTargetCount, value.ToList()); }

    /// <summary>修复目标数量（可单值或按等级）；与 TargetCondition 配合，从己方已摧毁物品中选取。</summary>
    public IntOrByTier RepairTargetCount { get => GetInt(KeyRepairTargetCount, 1); set => SetIntOrByTier(KeyRepairTargetCount, value.ToList()); }

    /// <summary>摧毁目标数量（可单值或按等级）；与 TargetCondition 配合，从己方未摧毁物品中选取，默认 1。</summary>
    public IntOrByTier DestroyTargetCount { get => GetInt(KeyDestroyTargetCount, 1); set => SetIntOrByTier(KeyDestroyTargetCount, value.ToList()); }

    /// <summary>增加/减少属性目标数量（可单值或按等级）；与 TargetCondition 配合；默认 20 表示对所有满足条件的物品生效，1、2… 表示至多选取该数量。</summary>
    public IntOrByTier ModifyAttributeTargetCount { get => GetInt(KeyModifyAttributeTargetCount, 20); set => SetIntOrByTier(KeyModifyAttributeTargetCount, value.ToList()); }

    /// <summary>吸血：1 表示造成伤害时按实际伤害量治疗己方，0 表示无。用于伤害效果。</summary>
    public IntOrByTier LifeSteal { get => GetInt(KeyLifeSteal, 0); set => SetIntOrByTier(KeyLifeSteal, value.ToList()); }

    /// <summary>自定义变量 0（可单值或按等级），用于如举重手套的武器伤害提升量等。</summary>
    public IntOrByTier Custom_0 { get => GetInt(KeyCustom_0, 0); set => SetIntOrByTier(KeyCustom_0, value.ToList()); }

    /// <summary>自定义变量 1（可单值或按等级），用于如宇宙炫羽/巨龙翼的「开始飞行」目标数等，与 TargetCountKey 配合避免与 ModifyAttributeTargetCount 冲突。</summary>
    public IntOrByTier Custom_1 { get => GetInt(KeyCustom_1, 0); set => SetIntOrByTier(KeyCustom_1, value.ToList()); }

    /// <summary>自定义变量 2（可单值或按等级），可被 OverridableAttributes 覆盖，如龙涎香治疗公式中的乘数。</summary>
    public IntOrByTier Custom_2 { get => GetInt(KeyCustom_2, 0); set => SetIntOrByTier(KeyCustom_2, value.ToList()); }

    /// <summary>物品价值（可单值或按等级）；注册时按尺寸自动设置默认值（小 [1,2,4,8]、中 [2,4,8,16]、大 [3,6,12,24]），用于龙涎香等公式。</summary>
    public IntOrByTier Price { get => GetInt(KeyPrice, 0); set => SetIntOrByTier(KeyPrice, value.ToList()); }

    /// <summary>储存箱等效参数（可单值或按等级），用于如废品场长枪的「卡组小型物品数 + StashParameter」公式。默认 0。</summary>
    public IntOrByTier StashParameter { get => GetInt(KeyStashParameter, 0); set => SetIntOrByTier(KeyStashParameter, value.ToList()); }

    /// <summary>可被卡组复写的属性及按 tier 的默认值（nameof(属性) → IntOrByTier）；null 表示无。拖入卡组或改 tier 时用此初始化/更新 Overrides。</summary>
    public Dictionary<string, IntOrByTier>? OverridableAttributes { get; set; }

    public List<AbilityDefinition> Abilities { get; set; } = [];
    /// <summary>光环列表：当在战斗内读取属性并传入 IAuraContext 时，会按条件与公式叠加这些光环。</summary>
    public List<AuraDefinition> Auras { get; set; } = [];

    /// <summary>上游协同先验：能触发该物品的“上游”需满足的机制/标签（OR of ANDs）；null 表示无。</summary>
    public List<SynergyClause>? UpstreamRequirements { get; set; }
    /// <summary>下游协同先验：该物品效果目标的“下游”需满足的机制/标签（OR of ANDs），子句 Direction 表示目标在己方左/右/任意；null 表示无。</summary>
    public List<SynergyClause>? DownstreamRequirements { get; set; }
    /// <summary>邻居协同先验：希望相邻位置存在的物品类型/机制（OR of ANDs）；null 表示无。</summary>
    public List<SynergyClause>? NeighborPreference { get; set; }

    /// <summary>获取按等级扩展属性的只读副本，用于序列化或复制。</summary>
    public IReadOnlyDictionary<string, List<int>> GetIntsByTierSnapshot() =>
        _intsByTier.ToDictionary(kv => kv.Key, kv => new List<int>(kv.Value));
}
