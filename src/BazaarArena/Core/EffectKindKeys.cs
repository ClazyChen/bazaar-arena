namespace BazaarArena.Core;

/// <summary>EffectKind 的集中元数据：默认模板字段名与日志显示名，避免多处重复映射。</summary>
public static class EffectKindKeys
{
    /// <summary>解析数值时使用的默认模板字段名（无 ValueKey 时）；Other 为 Custom_0。</summary>
    public static string GetDefaultTemplateKey(EffectKind kind) => kind switch
    {
        EffectKind.Damage => nameof(ItemTemplate.Damage),
        EffectKind.Burn => nameof(ItemTemplate.Burn),
        EffectKind.Poison => nameof(ItemTemplate.Poison),
        EffectKind.Shield => nameof(ItemTemplate.Shield),
        EffectKind.Heal => nameof(ItemTemplate.Heal),
        EffectKind.Regen => "Regen",
        EffectKind.Charge => nameof(ItemTemplate.Charge),
        EffectKind.Other => nameof(ItemTemplate.Custom_0),
        _ => "",
    };

    /// <summary>战斗日志中该效果的显示名称；Other 返回空（由自定义处理器自行打日志）。</summary>
    public static string GetLogName(EffectKind kind) => kind switch
    {
        EffectKind.Damage => "伤害",
        EffectKind.Burn => "灼烧",
        EffectKind.Poison => "剧毒",
        EffectKind.Shield => "护盾",
        EffectKind.Heal => "治疗",
        EffectKind.Regen => "生命再生",
        EffectKind.Charge => "充能",
        EffectKind.Other => "",
        _ => "",
    };
}
