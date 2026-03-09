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

    /// <summary>描述中可用 {Cooldown} 表示冷却秒数，实际读 CooldownMs。</summary>
    private static string ResolveKey(string key) => key == "Cooldown" ? "CooldownMs" : key;

    private static bool IsSecondsKey(string key) => key == "CooldownMs";

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

    /// <summary>单 tier 替换：返回替换后的字符串及数值区段（含前缀后缀，用于加粗）。</summary>
    public static (string Result, List<(int Start, int Length)> ValueRanges) ReplacePlaceholdersSingle(
        ItemTemplate template, ItemTier tier, string text)
    {
        string result = text ?? "";
        var valueRanges = new List<(int Start, int Length)>();
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
            valueRanges.Add((idx, replaceStr.Length));
            offset += replaceStr.Length - length;
        }
        return (result, valueRanges);
    }

    /// <summary>全 tier 替换：返回替换后的字符串及每个数值段的（起始、长度、tier），前缀后缀含在段内。</summary>
    public static (string Result, List<(int Start, int Length, ItemTier Tier)> ValueRanges) ReplacePlaceholdersAllTiers(
        ItemTemplate template, string text)
    {
        string result = text ?? "";
        var valueRanges = new List<(int Start, int Length, ItemTier Tier)>();
        var snapshot = template.GetIntsByTierSnapshot();
        var matches = ParsePlaceholders(result);
        int offset = 0;
        foreach (var (index, length, prefix, key, suffix) in matches)
        {
            int idx = index + offset;
            string actualKey = ResolveKey(key);
            if (!snapshot.TryGetValue(actualKey, out var list) || list.Count == 0)
            {
                string seg = prefix + "0" + suffix;
                result = result.Remove(idx, length).Insert(idx, seg);
                valueRanges.Add((idx, seg.Length, ItemTier.Bronze));
                offset += seg.Length - length;
                continue;
            }
            var segments = list.Select(v => prefix + (IsSecondsKey(actualKey) ? FormatCooldownSeconds(v) : v.ToString()) + suffix).ToList();
            string replaceStr = string.Join(" » ", segments);
            result = result.Remove(idx, length).Insert(idx, replaceStr);
            int off = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                valueRanges.Add((idx + off, segments[i].Length, (ItemTier)i));
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

    /// <summary>从整行字符串与数值区段构建 Inlines：区段加粗，其余部分做关键词着色。singleTierBrush 不为 null 时区段用该画刷（卡组内）；null 时用 valueRangesWithTier 的 tier 画刷（物品池）。</summary>
    public static List<Inline> BuildLineInlines(
        string line,
        List<(int Start, int Length)> valueRanges,
        Brush? singleTierBrush,
        Func<ItemTier, Brush>? tierBrushGetter = null)
    {
        var inlines = new List<Inline>();
        if (string.IsNullOrEmpty(line)) return inlines;
        int pos = 0;
        foreach (var (start, len) in valueRanges.OrderBy(r => r.Start))
        {
            if (start > pos)
                inlines.AddRange(EffectKeywordFormatting.BuildInlines(line.Substring(pos, start - pos)));
            string valueText = line.Substring(start, len);
            var run = new Run(valueText) { FontWeight = FontWeights.Bold };
            if (singleTierBrush != null)
                run.Foreground = singleTierBrush;
            inlines.Add(run);
            pos = start + len;
        }
        if (pos < line.Length)
            inlines.AddRange(EffectKeywordFormatting.BuildInlines(line.Substring(pos)));
        return inlines;
    }

    /// <summary>全 tier 版：valueRanges 含 Tier，用 tierBrushGetter 上色。</summary>
    public static List<Inline> BuildLineInlinesWithTiers(
        string line,
        List<(int Start, int Length, ItemTier Tier)> valueRanges,
        Func<ItemTier, Brush> tierBrushGetter)
    {
        var inlines = new List<Inline>();
        if (string.IsNullOrEmpty(line)) return inlines;
        int pos = 0;
        foreach (var (start, len, tier) in valueRanges.OrderBy(r => r.Start))
        {
            if (start > pos)
                inlines.AddRange(EffectKeywordFormatting.BuildInlines(line.Substring(pos, start - pos)));
            var run = new Run(line.Substring(start, len))
            {
                FontWeight = FontWeights.Bold,
                Foreground = tierBrushGetter(tier),
            };
            inlines.Add(run);
            pos = start + len;
        }
        if (pos < line.Length)
            inlines.AddRange(EffectKeywordFormatting.BuildInlines(line.Substring(pos)));
        return inlines;
    }
}
