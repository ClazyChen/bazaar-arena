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

    public ItemTemplate? GetTemplate(string name) =>
        _templates.TryGetValue(name, out var t) ? t : null;

    /// <summary>获取所有已注册物品名称，供 UI 下拉等使用。</summary>
    public IReadOnlyList<string> GetAllNames() =>
        _templates.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();

    /// <summary>注册物品模板；会将当前 DefaultSize、DefaultMinTier 写入模板后存入。</summary>
    public void Register(ItemTemplate template)
    {
        template.Size = DefaultSize;
        template.MinTier = DefaultMinTier;
        _templates[template.Name] = template;
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
            Tags = [..t.Tags],
            Abilities = [.. t.Abilities.Select(a => new AbilityDefinition
            {
                TriggerName = a.TriggerName,
                Priority = a.Priority,
                Condition = EnsureTriggerCondition(a.TriggerName, Condition.Clone(a.Condition)),
                TargetCondition = Condition.Clone(a.TargetCondition),
                Effects = a.Effects.Select(e => new EffectDefinition { Value = e.Value, ValueResolver = e.ValueResolver, ValueKey = e.ValueKey, ApplyCritMultiplier = e.ApplyCritMultiplier, Apply = e.Apply }).ToList(),
            })],
            Auras = t.Auras.Select(a => new AuraDefinition { AttributeName = a.AttributeName, Condition = Condition.Clone(a.Condition), FixedValueKey = a.FixedValueKey, PercentValueKey = a.PercentValueKey, FixedValueFormula = a.FixedValueFormula }).ToList(),
            OverridableAttributes = t.OverridableAttributes != null ? new Dictionary<string, IntOrByTier>(t.OverridableAttributes) : null,
        };
        clone.SetIntsByTier(t.GetIntsByTierSnapshot());
        return clone;
    }

    /// <summary>UseItem → SameAsSource；UseOtherItem 始终叠加己方其他物品（And(DifferentFromSource, SameSide)），再与显式 Condition（如 WithTag）取与；Freeze → SameSide（己方触发冻结时）。</summary>
    private static Condition? EnsureTriggerCondition(string triggerName, Condition? condition)
    {
        if (triggerName == Trigger.UseItem) return condition ?? Condition.SameAsSource;
        if (triggerName == Trigger.UseOtherItem)
        {
            Condition baseSameSideOther = Condition.And(Condition.DifferentFromSource, Condition.SameSide);
            return condition != null ? Condition.And(baseSameSideOther, condition) : baseSameSideOther;
        }
        if (triggerName == Trigger.Freeze) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.Slow) return condition ?? Condition.SameSide;
        if (triggerName == Trigger.OnDestroy) return condition ?? Condition.SameSide;
        return condition;
    }
}
