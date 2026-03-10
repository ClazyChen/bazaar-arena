using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using BazaarArena.Core;

namespace BazaarArena;

/// <summary>物品描述占位符替换与 ToolTip 行构建：支持 {Key}、{+Custom_0%} 等（前缀/后缀随数值一并加粗着色）。</summary>
public static class ItemDescHelper
{
    /// <summary>大括号内：可选前缀(非字母数字下划线) + key(字母数字下划线) + 可选后缀(非字母数字下划线)。</summary>
    private static readonly Regex PlaceholderRegex = new(@"\{([^a-zA-Z0-9_]*)([a-zA-Z0-9_]+)([^a-zA-Z0-9_]*)\}", RegexOptions.Compiled);

    /// <summary>描述中可用 {Cooldown} 表示冷却秒数，实际读 CooldownMs；{ChargeSeconds} 表示充能秒数，实际读 Charge（毫秒）；{FreezeSeconds} 表示冻结秒数，实际读 Freeze（毫秒）；{SlowSeconds} 表示减速秒数，实际读 Slow（毫秒）；{HasteSeconds} 表示加速秒数，实际读 Haste（毫秒）。</summary>
    private static string ResolveKey(string key) => key switch
    {
        "Cooldown" => "CooldownMs",
        "ChargeSeconds" => "Charge",
        "FreezeSeconds" => "Freeze",
        "SlowSeconds" => "Slow",
        "HasteSeconds" => "Haste",
        _ => key,
    };

    private static bool IsSecondsKey(string key) => key == "CooldownMs" || key == "Charge" || key == "Freeze" || key == "Slow" || key == "Haste";

    /// <summary>解析文本中所有占位符，返回 (索引, 占位符全长, 前缀, Key, 后缀)。</summary>
    private static List<(int Index, int Length, string Prefix, string Key, string Suffix)> ParsePlaceholders(string text)
    {
        var list = new List<(int Index, int Length, string Prefix, string Key, string Suffix)>();
        if (string.IsNullOrEmpty(text)) return list;
        foreach (Match m in PlaceholderRegex.Matches(text))
        {
            list.Add((m.Index, m.Length, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value));
        }
        return list.OrderBy(x => x.Index).ToList();
    }

    /// <summary>单 tier 替换：返回替换后的字符串及数值区段（含前缀后缀，用于加粗）。ChargeSeconds 的数值使用充能色画刷。</summary>
    public static (string Result, List<(int Start, int Length, Brush? OverrideBrush)> ValueRanges) ReplacePlaceholdersSingle(
        ItemTemplate template, ItemTier tier, string text)
    {
        string result = text ?? "";
        var valueRanges = new List<(int Start, int Length, Brush? OverrideBrush)>();
        var matches = ParsePlaceholders(result);
        int offset = 0;
        foreach (var (index, length, prefix, key, suffix) in matches)
        {
            int idx = index + offset;
            string actualKey = ResolveKey(key);
            int value = template.GetInt(actualKey, tier);
            string valueStr = IsSecondsKey(actualKey) ? FormatCooldownSeconds(value) : value.ToString();
            string replaceStr = prefix + valueStr + suffix;
            result = result.Remove(idx, length).Insert(idx, replaceStr);
            valueRanges.Add((idx, replaceStr.Length, (Brush?)null));
            offset += replaceStr.Length - length;
        }
        return (result, valueRanges);
    }

    /// <summary>全 tier 替换：返回替换后的字符串及每个数值段的（起始、长度、tier、可选画刷）。不随 tier 变化的单值可传入 singleValueBrush 以用白色等显示。</summary>
    public static (string Result, List<(int Start, int Length, ItemTier Tier, Brush? OverrideBrush)> ValueRanges) ReplacePlaceholdersAllTiers(
        ItemTemplate template, string text, Brush? singleValueBrush = null)
    {
        string result = text ?? "";
        var valueRanges = new List<(int Start, int Length, ItemTier Tier, Brush? OverrideBrush)>();
        var snapshot = template.GetIntsByTierSnapshot();
        var matches = ParsePlaceholders(result);
        int offset = 0;
        foreach (var (index, length, prefix, key, suffix) in matches)
        {
            int idx = index + offset;
            string actualKey = ResolveKey(key);
            if (!snapshot.TryGetValue(actualKey, out var list) || list.Count == 0)
            {
                string seg = prefix + (IsSecondsKey(actualKey) ? FormatCooldownSeconds(0) : "0") + suffix;
                result = result.Remove(idx, length).Insert(idx, seg);
                valueRanges.Add((idx, seg.Length, ItemTier.Bronze, singleValueBrush));
                offset += seg.Length - length;
                continue;
            }
            var segments = list.Select(v => prefix + (IsSecondsKey(actualKey) ? FormatCooldownSeconds(v) : v.ToString()) + suffix).ToList();
            string replaceStr = string.Join(" » ", segments);
            result = result.Remove(idx, length).Insert(idx, replaceStr);
            bool isSingleValue = list.Count == 1;
            int off = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                valueRanges.Add((idx + off, segments[i].Length, (ItemTier)i, isSingleValue ? singleValueBrush : null));
                off += segments[i].Length + (i < segments.Count - 1 ? 3 : 0); // " » "
            }
            offset += replaceStr.Length - length;
        }
        return (result, valueRanges);
    }

    private static string FormatCooldownSeconds(int cooldownMs)
    {
        double sec = cooldownMs / 1000.0;
        return sec == (int)sec ? ((int)sec).ToString() : sec.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>从整行字符串与数值区段构建 Inlines：区段加粗，其余部分做关键词着色。OverrideBrush 优先，否则 singleTierBrush（卡组内）或 tier 画刷（物品池）。</summary>
    public static List<Inline> BuildLineInlines(
        string line,
        List<(int Start, int Length, Brush? OverrideBrush)> valueRanges,
        Brush? singleTierBrush,
        Func<ItemTier, Brush>? tierBrushGetter = null)
    {
        var inlines = new List<Inline>();
        if (string.IsNullOrEmpty(line)) return inlines;
        int pos = 0;
        foreach (var (start, len, overrideBrush) in valueRanges.OrderBy(r => r.Start))
        {
            if (start > pos)
                inlines.AddRange(EffectKeywordFormatting.BuildInlines(line.Substring(pos, start - pos)));
            string valueText = line.Substring(start, len);
            var run = new Run(valueText) { FontWeight = FontWeights.Bold };
            var brush = overrideBrush ?? singleTierBrush;
            if (brush != null)
                run.Foreground = brush;
            inlines.Add(run);
            pos = start + len;
        }
        if (pos < line.Length)
            inlines.AddRange(EffectKeywordFormatting.BuildInlines(line.Substring(pos)));
        return inlines;
    }

    /// <summary>全 tier 版：valueRanges 含 Tier 与可选 OverrideBrush（如充能色），OverrideBrush 优先于 tier 画刷。</summary>
    public static List<Inline> BuildLineInlinesWithTiers(
        string line,
        List<(int Start, int Length, ItemTier Tier, Brush? OverrideBrush)> valueRanges,
        Func<ItemTier, Brush> tierBrushGetter)
    {
        var inlines = new List<Inline>();
        if (string.IsNullOrEmpty(line)) return inlines;
        int pos = 0;
        foreach (var (start, len, tier, overrideBrush) in valueRanges.OrderBy(r => r.Start))
        {
            if (start > pos)
                inlines.AddRange(EffectKeywordFormatting.BuildInlines(line.Substring(pos, start - pos)));
            var run = new Run(line.Substring(start, len))
            {
                FontWeight = FontWeights.Bold,
                Foreground = overrideBrush ?? tierBrushGetter(tier),
            };
            inlines.Add(run);
            pos = start + len;
        }
        if (pos < line.Length)
            inlines.AddRange(EffectKeywordFormatting.BuildInlines(line.Substring(pos)));
        return inlines;
    }
}
