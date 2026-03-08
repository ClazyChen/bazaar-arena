using BazaarArena.Core;

namespace BazaarArena.ItemDatabase;

/// <summary>物品数据库：按中文名称创建物品模板（工厂模式）。</summary>
public class ItemDatabase : IItemTemplateResolver
{
    private readonly Dictionary<string, ItemTemplate> _templates = new();

    public ItemTemplate? GetTemplate(string name) =>
        _templates.TryGetValue(name, out var t) ? t : null;

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
            MinTier = t.MinTier,
            Size = t.Size,
            Tags = [..t.Tags],
            Abilities = t.Abilities.Select(a => new AbilityDefinition
            {
                TriggerName = a.TriggerName,
                Priority = a.Priority,
                Effects = a.Effects.Select(e => new EffectDefinition { Kind = e.Kind, Value = e.Value }).ToList(),
            }).ToList(),
            Auras = [..t.Auras],
        };
        clone.SetIntsByTier(t.GetIntsByTierSnapshot());
        return clone;
    }
}
