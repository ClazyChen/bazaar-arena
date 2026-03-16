using System.Text.RegularExpressions;
using BazaarArena.Core;

namespace BazaarArena.ItemDatabase;

/// <summary>物品数据库：按中文名称创建物品模板（工厂模式）。</summary>
public class ItemDatabase : IItemTemplateResolver
{
    private readonly Dictionary<string, ItemTemplate> _templates = new();

    /// <summary>注册时用于填充模板的默认尺寸；在 RegisterAll 中按批次设置（如先 Small 再注册所有小物品）。</summary>
    public ItemSize DefaultSize { get; set; } = ItemSize.Small;

    /// <summary>注册时用于填充模板的默认最低档位；在 RegisterAll 中按批次设置（如 Bronze 注册完再设为 Silver）。</summary>
    public ItemTier DefaultMinTier { get; set; } = ItemTier.Bronze;

    /// <summary>注册时用于填充模板的默认英雄；在 RegisterAll 中按批次设置，避免同一文件中反复定义。</summary>
    public string DefaultHero { get; set; } = Hero.Common;

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

    /// <summary>按尺寸返回 Price 默认值：小 [1,2,4,8]、中 [2,4,8,16]、大 [3,6,12,24]，供 Register 自动设置。</summary>
    private static IntOrByTier GetDefaultPriceBySize(ItemSize size) => size switch
    {
        ItemSize.Small => [1, 2, 4, 8],
        ItemSize.Medium => [2, 4, 8, 16],
        ItemSize.Large => [3, 6, 12, 24],
        _ => [1, 2, 4, 8],
    };

    /// <summary>注册物品模板；会将当前 DefaultSize、DefaultMinTier、DefaultHero 写入模板后存入，并根据属性自动补充类型 Tag（护盾/伤害/灼烧等）。若存在 OverridableAttributes，将其默认值同步到模板对应 key，避免在模板上重复定义同一数值。Price 在注册时按 DefaultSize 自动设置默认值。</summary>
    public void Register(ItemTemplate template)
    {
        template.Size = DefaultSize;
        template.MinTier = DefaultMinTier;
        template.Hero = DefaultHero;
        template.SetIntOrByTierByKey(Key.Price, GetDefaultPriceBySize(DefaultSize));
        if (template.OverridableAttributes != null)
        {
            foreach (var kv in template.OverridableAttributes)
                template.SetIntOrByTierByKey(kv.Key, kv.Value);
        }
        EnsureTypeTags(template);
        _templates[template.Name] = template;
    }

    /// <summary>根据模板数值属性为任一档位 &gt; 0 时自动加入对应类型 Tag；若属性为 0 但存在作用目标为自身（SameAsSource）的光环且光环 AttributeName 为类型属性，也补充对应 Tag。供 Condition 与可暴击判定使用。</summary>
    private static void EnsureTypeTags(ItemTemplate template)
    {
        if (template.Size == ItemSize.Small) TryAddTag(template, Tag.Small);
        else if (template.Size == ItemSize.Medium) TryAddTag(template, Tag.Medium);
        else if (template.Size == ItemSize.Large) TryAddTag(template, Tag.Large);

        if (HasAnyTierPositive(template, Key.Damage)) TryAddTag(template, Tag.Damage);
        if (HasAnyTierPositive(template, Key.Burn)) TryAddTag(template, Tag.Burn);
        if (HasAnyTierPositive(template, Key.Poison)) TryAddTag(template, Tag.Poison);
        if (HasAnyTierPositive(template, Key.Heal)) TryAddTag(template, Tag.Heal);
        if (HasAnyTierPositive(template, Key.Shield)) TryAddTag(template, Tag.Shield);
        if (HasAnyTierPositive(template, Key.AmmoCap)) TryAddTag(template, Tag.Ammo);
        if (HasAnyTierPositive(template, "Regen")) TryAddTag(template, Tag.Regen);

        foreach (var aura in template.Auras ?? [])
        {
            if (aura.Condition != Condition.SameAsSource) continue;
            var tag = AttributeNameToTypeTag(aura.AttributeName);
            if (tag != null) TryAddTag(template, tag);
        }
    }

    /// <summary>将光环 AttributeName 映射为类型 Tag；非类型属性返回 null。</summary>
    private static string? AttributeNameToTypeTag(string attributeName)
    {
        return attributeName switch
        {
            Key.Damage => Tag.Damage,
            Key.Burn => Tag.Burn,
            Key.Poison => Tag.Poison,
            Key.Heal => Tag.Heal,
            Key.Shield => Tag.Shield,
            "Regen" => Tag.Regen,
            _ => null,
        };
    }

    private static bool HasAnyTierPositive(ItemTemplate t, string key)
    {
        foreach (ItemTier tier in Enum.GetValues<ItemTier>())
            if (t.GetInt(key, tier, 0) > 0) return true;
        return false;
    }

    private static void TryAddTag(ItemTemplate template, string tag)
    {
        if (template.Tags == null) template.Tags = [];
        if (!template.Tags.Contains(tag)) template.Tags.Add(tag);
    }

    /// <summary>根据名称创建模板副本（用于对战中的实例，可叠加局外重写）。</summary>
    public ItemTemplate? CreateTemplate(string name)
    {
        var t = GetTemplate(name);
        return t == null ? null : CloneTemplate(t);
    }

    private static ItemTemplate CloneTemplate(ItemTemplate t)
    {
        var clone = new ItemTemplate
        {
            Name = t.Name,
            Desc = t.Desc,
            MinTier = t.MinTier,
            Size = t.Size,
            Hero = t.Hero,
            Tags = [..t.Tags],
            Abilities = [.. t.Abilities.Select(a =>
            {
                var def = new AbilityDefinition
                {
                    TriggerName = a.TriggerName,
                    Priority = a.Priority,
                    Condition = EnsureTriggerCondition(a.TriggerName, Condition.Clone(a.Condition)),
                    SourceCondition = Condition.Clone(a.SourceCondition),
                    InvokeTargetCondition = Condition.Clone(a.InvokeTargetCondition),
                    TargetCondition = Condition.Clone(a.TargetCondition),
                    Value = a.Value,
                    ValueKey = a.ValueKey,
                    ApplyCritMultiplier = a.ApplyCritMultiplier,
                    UseSelf = a.UseSelf,
                    Apply = a.Apply,
                    EffectLogName = a.EffectLogName,
                    TargetCountKey = a.TargetCountKey,
                    Triggers = a.Triggers?.Select(e => new AbilityDefinition.TriggerEntry
                    {
                        TriggerName = e.TriggerName,
                        Condition = Condition.Clone(e.Condition),
                        SourceCondition = Condition.Clone(e.SourceCondition),
                        InvokeTargetCondition = Condition.Clone(e.InvokeTargetCondition),
                    }).ToList(),
                };
                def.EnsureTriggersInitializedFromTopLevel();
                return def;
            })],
            Auras = t.Auras.Select(a => new AuraDefinition { AttributeName = a.AttributeName, Condition = Condition.Clone(a.Condition), SourceCondition = Condition.Clone(a.SourceCondition), Value = a.Value, Percent = a.Percent }).ToList(),
            OverridableAttributes = t.OverridableAttributes != null ? new Dictionary<string, IntOrByTier>(t.OverridableAttributes) : null,
        };
        clone.SetIntsByTier(t.GetIntsByTierSnapshot());
        return clone;
    }

    /// <summary>condition ?? default：UseItem → SameAsSource，Freeze/Slow/Haste/Crit/Destroy/Burn → SameSide，BattleStart → Always。</summary>
    private static Condition? EnsureTriggerCondition(string triggerName, Condition? condition)
    {
        if (triggerName == Trigger.UseItem) return condition ?? Condition.SameAsSource;
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
        if (triggerName == Trigger.AboutToLose) return condition ?? Condition.SameSide;
        return condition;
    }
}
