namespace BazaarArena.Core;

/// <summary>EffectKind 的扩展方法：将默认 key、日志名等元数据强绑定到枚举，便于调用方使用。</summary>
public static class EffectKindExtensions
{
    /// <summary>解析数值时使用的默认模板字段名（无 ValueKey 时）；Other 为 Custom_0。</summary>
    public static string GetDefaultTemplateKey(this EffectKind kind) =>
        EffectKindKeys.GetDefaultTemplateKey(kind);

    /// <summary>战斗日志中该效果的显示名称；Other 返回空（由自定义处理器自行打日志）。</summary>
    public static string GetLogName(this EffectKind kind) =>
        EffectKindKeys.GetLogName(kind);
}
