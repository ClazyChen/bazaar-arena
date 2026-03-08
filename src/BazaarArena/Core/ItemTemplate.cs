namespace BazaarArena.Core;

/// <summary>物品模板：名称、最低等级、尺寸、标签、属性、能力与光环。由物品数据库按名称创建实例。</summary>
public class ItemTemplate
{
    public string Name { get; set; } = "";
    public ItemTier MinTier { get; set; }
    public ItemSize Size { get; set; }
    public List<string> Tags { get; set; } = new();

    /// <summary>冷却时间（毫秒）。设计文档：最低只能减到 1 秒。</summary>
    public int CooldownMs { get; set; }

    /// <summary>暴击率（百分比，0–100）。</summary>
    public int CritRatePercent { get; set; }

    /// <summary>多重触发，默认 1。</summary>
    public int Multicast { get; set; } = 1;

    /// <summary>弹药上限，0 表示不依赖弹药。</summary>
    public int AmmoCap { get; set; }

    public List<AbilityDefinition> Abilities { get; set; } = new();
    /// <summary>光环列表（基座阶段可留空，后续按属性、条件、百分比加算实现）。</summary>
    public List<object> Auras { get; set; } = new();
}
