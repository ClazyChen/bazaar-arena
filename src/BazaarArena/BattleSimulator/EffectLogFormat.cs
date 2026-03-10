namespace BazaarArena.BattleSimulator;

/// <summary>战斗日志中效果数值的显示格式（如充能显示为「1 秒」而非「1000」）。</summary>
public static class EffectLogFormat
{
    /// <summary>返回效果数值的日志显示字符串。充能、冻结、减速、加速（毫秒）格式化为「N 秒」；开始飞行等无数值效果返回空。</summary>
    public static string FormatEffectValue(string effectKind, int value)
    {
        if (effectKind == "充能" || effectKind == "冻结" || effectKind == "减速" || effectKind == "加速")
        {
            if (value % 1000 == 0)
                return $"{value / 1000} 秒";
            return $"{value / 1000.0:F1} 秒";
        }
        if (effectKind == "开始飞行")
            return "";
        return value.ToString();
    }
}
