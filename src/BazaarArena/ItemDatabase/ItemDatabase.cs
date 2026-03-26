using System.Text.RegularExpressions;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase;

/// <summary>物品数据库：按中文名称创建物品模板（工厂模式）。</summary>
public class ItemDatabase : IItemTemplateResolver
{
    private readonly Dictionary<string, ItemTemplate> _templates = new();

    /// <summary>注册时用于填充模板的默认尺寸；在 RegisterAll 中按批次设置（如先 Small 再注册所有小物品）。</summary>
    public int DefaultSize { get; set; } = ItemSize.Small;

    /// <summary>注册时用于填充模板的默认最低档位；在 RegisterAll 中按批次设置（如 Bronze 注册完再设为 Silver）。</summary>
    public ItemTier DefaultMinTier { get; set; } = ItemTier.Bronze;

    /// <summary>注册时用于填充模板的默认英雄；在 RegisterAll 中按批次设置，避免同一文件中反复定义。</summary>
    public int DefaultHero { get; set; } = Hero.Common;

    public ItemTemplate? GetTemplate(string name) =>
        _templates.TryGetValue(name, out var t) ? t : null;

    /// <summary>获取所有已注册物品名称，供 UI 下拉等使用。</summary>
    public IReadOnlyList<string> GetAllNames() =>
        _templates.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();

    /// <summary>去掉末尾 _S\d+ 得到基名；无后缀则返回原名。多版本约定：无后缀=最新，_Sx=第 x 赛季。</summary>
    public static string GetBaseName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name ?? "";
        var m = Regex.Match(name.Trim(), @"^(.+)_S(\d+)$");
        return m.Success ? m.Groups[1].Value : name.Trim();
    }

    /// <summary>名称匹配 _S\d+$ 视为历史版本。</summary>
    public static bool IsHistoricalVersion(string name) =>
        !string.IsNullOrWhiteSpace(name) && Regex.IsMatch(name.Trim(), @"_S\d+$");

    /// <summary>获取仅“最新版本”的名称列表（不含 _Sx 后缀），供 GUI 默认物品池使用。</summary>
    public IReadOnlyList<string> GetLatestOnlyNames() =>
        _templates.Keys.Where(n => !IsHistoricalVersion(n)).OrderBy(x => x, StringComparer.Ordinal).ToList();

    /// <summary>该基名对应的所有版本名，顺序：无后缀（若存在）在前，其余按 _S 后数字降序（S10 在 S7 前）。</summary>
    public IReadOnlyList<string> GetVersionCycle(string baseName)
    {
        if (string.IsNullOrWhiteSpace(baseName)) return [];
        var baseTrim = baseName.Trim();
        var withBase = _templates.ContainsKey(baseTrim);
        var historical = _templates.Keys
            .Where(k => k.StartsWith(baseTrim + "_S", StringComparison.Ordinal) && Regex.IsMatch(k, @"_S\d+$"))
            .Select(k => (Name: k, Num: int.Parse(Regex.Match(k, @"_S(\d+)$").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)))
            .OrderByDescending(x => x.Num)
            .Select(x => x.Name)
            .ToList();
        if (!withBase && historical.Count == 0) return [];
        var list = new List<string>();
        if (withBase) list.Add(baseTrim);
        list.AddRange(historical);
        return list;
    }

    /// <summary>按尺寸返回 <see cref="Key.Value"/>（局外/模板默认价值）默认值：小 [1,2,4,8]、中 [2,4,8,16]、大 [3,6,12,24]，由 <see cref="EnsureDefaultAttributes"/> 在注册时写入。</summary>
    private static IntOrByTier GetDefaultPriceBySize(int size) => size switch
    {
        ItemSize.Small => [1, 2, 4, 8],
        ItemSize.Medium => [2, 4, 8, 16],
        ItemSize.Large => [3, 6, 12, 24],
        _ => [1, 2, 4, 8],
    };

    private static bool IsUndefined(ItemTemplate template, int key) =>
        template.GetIntsByTierSnapshot().TryGetValue(key, out var values) && values.Count == 0;

    private static IntOrByTier TrimByMinTier(IntOrByTier values, ItemTier minTier)
    {
        var list = values.ToList();
        int skip = (int)minTier;
        if (skip <= 0) return [.. list];
        if (skip >= list.Count) return [list[^1]];
        return [.. list.Skip(skip)];
    }

    private static void EnsureDefaultIfUndefined(ItemTemplate template, int key, IntOrByTier defaultValue)
    {
        if (IsUndefined(template, key))
            template.SetIntOrByTierByKey(key, defaultValue);
    }

    private static void EnsureDefaultAttributes(ItemTemplate template, int defaultSize)
    {
        EnsureDefaultIfUndefined(template, Key.Multicast, 1);
        EnsureDefaultIfUndefined(template, Key.CritDamage, 200);
        EnsureDefaultIfUndefined(template, Key.ChargeTargetCount, 1);
        EnsureDefaultIfUndefined(template, Key.HasteTargetCount, 20);
        EnsureDefaultIfUndefined(template, Key.SlowTargetCount, 20);
        EnsureDefaultIfUndefined(template, Key.FreezeTargetCount, 1);
        EnsureDefaultIfUndefined(template, Key.ReloadTargetCount, 1);
        EnsureDefaultIfUndefined(template, Key.RepairTargetCount, 1);
        EnsureDefaultIfUndefined(template, Key.DestroyTargetCount, 1);
        EnsureDefaultIfUndefined(template, Key.ModifyAttributeTargetCount, 20);
        EnsureDefaultIfUndefined(template, Key.Value, TrimByMinTier(GetDefaultPriceBySize(defaultSize), template.MinTier));
    }

    /// <summary>注册物品模板；会将当前 DefaultSize、DefaultMinTier、DefaultHero 写入模板后存入，并根据属性自动补充类型 Tag（护盾/伤害/灼烧等）。若存在 OverridableAttributes，将其默认值同步到模板对应 key，避免在模板上重复定义同一数值。默认「价值」由 <see cref="EnsureDefaultAttributes"/> 写入 <see cref="Key.Value"/>（见 <see cref="GetDefaultPriceBySize"/>），与 <see cref="Key.Custom_0"/> 等 gameplay 字段分离。</summary>
    public void Register(ItemTemplate template)
    {
        template.Size = DefaultSize;
        template.MinTier = DefaultMinTier;
        template.Hero = DefaultHero;
        EnsureDefaultAttributes(template, DefaultSize);
        if (template.OverridableAttributes != null)
        {
            foreach (var kv in template.OverridableAttributes)
                template.SetIntOrByTierByKey(kv.Key, kv.Value);
        }
        NormalizeAbilities(template);
        EnsureTypeTags(template);
        _templates[template.Name] = template;
    }

    /// <summary>
    /// 注册阶段一次性规范化能力触发配置：
    /// - 补齐主字段 Condition 的默认值（与模拟器一致）
    /// - 保证 Triggers 非空且首条与主字段一致
    /// </summary>
    private static void NormalizeAbilities(ItemTemplate template)
    {
        foreach (var a in template.Abilities ?? [])
        {
            if (a.TriggerEntries == null || a.TriggerEntries.Count == 0)
                a.TriggerEntries = [new TriggerEntry { Trigger = Trigger.UseItem, Condition = Condition.SameAsCaster }];
            for (int i = 0; i < a.TriggerEntries.Count; i++)
            {
                var e = a.TriggerEntries[i];
                e.Condition = EnsureTriggerCondition(e.Trigger, e.Condition);
                a.TriggerEntries[i] = e;
            }
        }
    }

    /// <summary>根据 Ability Apply 类型、SameAsCaster 光环、可暴击与冷却自动补充类型 Tag 与 Crit/Cooldown。供 Condition 与可暴击判定使用。</summary>
    private static void EnsureTypeTags(ItemTemplate template)
    {
        if (template.Size == ItemSize.Small) TryAddTag(template, Tag.Small);
        else if (template.Size == ItemSize.Medium) TryAddTag(template, Tag.Medium);
        else if (template.Size == ItemSize.Large) TryAddTag(template, Tag.Large);

        if (HasAnyTierPositive(template, Key.AmmoCap)) TryAddDerivedTag(template, DerivedTag.Ammo);

        // 类型/机制 Tag：由 Ability 的 Apply 类型决定（与 MechanicTagger 规则一致）
        foreach (var a in template.Abilities ?? [])
        {
            var typeTag = AbilityTypeToTypeTag(a.AbilityType);
            if (typeTag != 0) TryAddDerivedTag(template, typeTag);
        }

        foreach (var aura in template.Auras ?? [])
        {
            if (aura.Condition != Condition.SameAsCaster) continue;
            var tag = AttributeToTypeTag(aura.Attribute);
            if (tag != 0) TryAddDerivedTag(template, tag);
        }

        bool canCrit = HasAnyCrittableTag(template) && HasUseItemSelfCritAbility(template);
        // Tag.Crit：具备六类可暴击 Tag 之一且至少一条 UseItem+ApplyCritMultiplier 能力
        if (canCrit)
            TryAddDerivedTag(template, DerivedTag.Crit);
        template.SetIntByKey(Key.CanCrit, canCrit ? 1 : 0);

        if (HasAnyTierPositive(template, Key.CooldownMs)) TryAddDerivedTag(template, DerivedTag.Cooldown);
    }

    private static bool HasAnyCrittableTag(ItemTemplate template)
    {
        var tags = GetDerivedTagMask(template);
        int crittableMask = DerivedTag.Damage | DerivedTag.Burn | DerivedTag.Poison | DerivedTag.Heal | DerivedTag.Shield | DerivedTag.Regen;
        return (tags & crittableMask) != 0;
    }

    private static bool HasUseItemSelfCritAbility(ItemTemplate template)
    {
        foreach (var a in template.Abilities ?? [])
        {
            if (a.Apply == null || !a.ApplyCritMultiplier) continue;
            if (a.TriggerEntries.Any(e => e.Trigger == Trigger.UseItem)) return true;
        }
        return false;
    }

    private static int AbilityTypeToTypeTag(AbilityType abilityType) => abilityType switch
    {
        AbilityType.Damage => DerivedTag.Damage,
        AbilityType.Burn => DerivedTag.Burn,
        AbilityType.Poison => DerivedTag.Poison,
        AbilityType.Heal => DerivedTag.Heal,
        AbilityType.Shield => DerivedTag.Shield,
        AbilityType.Charge => DerivedTag.Charge,
        AbilityType.Freeze => DerivedTag.Freeze,
        AbilityType.Slow => DerivedTag.Slow,
        AbilityType.Haste => DerivedTag.Haste,
        AbilityType.Reload => DerivedTag.Reload,
        _ => 0,
    };

    /// <summary>将光环 Attribute 映射为类型 Tag；非类型属性返回 0。</summary>
    private static int AttributeToTypeTag(int attribute)
    {
        return attribute switch
        {
            Key.Damage => DerivedTag.Damage,
            Key.Burn => DerivedTag.Burn,
            Key.Poison => DerivedTag.Poison,
            Key.Heal => DerivedTag.Heal,
            Key.Shield => DerivedTag.Shield,
            Key.Regen => DerivedTag.Regen,
            _ => 0,
        };
    }

    private static bool HasAnyTierPositive(ItemTemplate t, int key)
    {
        foreach (ItemTier tier in Enum.GetValues<ItemTier>())
            if (t.GetInt(key, tier) > 0) return true;
        return false;
    }

    private static int GetTagMask(ItemTemplate template) =>
        template.GetInt(Key.Tags, template.MinTier);

    private static void SetTagMask(ItemTemplate template, int tagMask) =>
        template.SetIntByKey(Key.Tags, tagMask);

    private static void TryAddTag(ItemTemplate template, int tagMask)
    {
        SetTagMask(template, GetTagMask(template) | tagMask);
    }

    private static int GetDerivedTagMask(ItemTemplate template) =>
        template.GetInt(Key.DerivedTags, template.MinTier);

    private static void SetDerivedTagMask(ItemTemplate template, int tagMask) =>
        template.SetIntByKey(Key.DerivedTags, tagMask);

    private static void TryAddDerivedTag(ItemTemplate template, int tagMask)
    {
        SetDerivedTagMask(template, GetDerivedTagMask(template) | tagMask);
    }

    /// <summary>condition ?? default：UseItem → SameAsCaster，UseOtherItem → SameSide&DifferentFromCaster，Freeze/Slow/Haste/Crit/Destroy/Burn → SameSide，BattleStart → Always。</summary>
    private static Formula EnsureTriggerCondition(int triggerName, Formula? condition)
    {
        if (triggerName == Trigger.UseItem) return condition ?? Condition.SameAsCaster;
        if (triggerName == Trigger.UseOtherItem) return condition ?? (Condition.SameSide & Condition.DifferentFromCaster);
        if (triggerName == Trigger.Freeze) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Slow) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Haste) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Crit) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Destroy) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Burn) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Poison) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Shield) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.BattleStart) return condition ?? Condition.Always;
        if (triggerName == Trigger.Ammo) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Reload) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.AboutToLose) return condition ?? Condition.CasterSideHpLEZero;
        if (triggerName == Trigger.CritRateIncreased) return condition ?? Condition.SameSide;
        return condition ?? Condition.Always;
    }
}
