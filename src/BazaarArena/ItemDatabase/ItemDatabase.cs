using BazaarArena.Core;

namespace BazaarArena.ItemDatabase;

/// <summary>物品数据库：按中文名称创建物品模板（工厂模式）。</summary>
public class ItemDatabase : IItemTemplateResolver
{
    private readonly Dictionary<string, ItemTemplate> _templates = new();

    public ItemTemplate? GetTemplate(string name) =>
        _templates.TryGetValue(name, out var t) ? t : null;

    /// <summary>获取所有已注册物品名称，供 UI 下拉等使用。</summary>
    public IReadOnlyList<string> GetAllNames() =>
        _templates.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();

    /// <summary>注册物品模板。</summary>
    public void Register(ItemTemplate template)
    {
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
                Condition = EnsureTriggerCondition(a.TriggerName, CloneCondition(a.Condition)),
                Effects = a.Effects.Select(e => new EffectDefinition { Kind = e.Kind, Value = e.Value, ValueResolver = e.ValueResolver, ValueKey = e.ValueKey, CustomEffectId = e.CustomEffectId }).ToList(),
            })],
            Auras = t.Auras.Select(a => new AuraDefinition { AttributeName = a.AttributeName, Condition = CloneCondition(a.Condition), FixedValueKey = a.FixedValueKey, PercentValueKey = a.PercentValueKey }).ToList(),
        };
        clone.SetIntsByTier(t.GetIntsByTierSnapshot());
        return clone;
    }

    private static Condition? CloneCondition(Condition? c) =>
        c == null ? null : new Condition { Kind = c.Kind, Tag = c.Tag };

    private static Condition? EnsureTriggerCondition(string triggerName, Condition? condition)
    {
        if (condition != null) return condition;
        if (triggerName == Trigger.UseItem) return Condition.SameAsSource;
        if (triggerName == Trigger.UseOtherItem) return Condition.DifferentFromSource;
        return null;
    }
}
