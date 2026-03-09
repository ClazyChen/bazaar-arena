using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using BazaarArena.Core;

namespace BazaarArena;

/// <summary>物品描述占位符替换与 ToolTip 行构建：{Damage}、{Cooldown} 等，单 tier 或全 tier。</summary>
public static class ItemDescHelper
{
    private static readonly (string Placeholder, string Key, bool AsSeconds)[] Placeholders =
    [
        ("{Damage}", "Damage", false),
        ("{Cooldown}", "CooldownMs", true),
        ("{Burn}", "Burn", false),
        ("{Poison}", "Poison", false),
        ("{Shield}", "Shield", false),
        ("{Heal}", "Heal", false),
        ("{Regen}", "Regen", false),
    ];

    /// <summary>单 tier 替换：返回替换后的字符串及数值区段（起始索引、长度），用于加粗。</summary>
    public static (string Result, List<(int Start, int Length)> ValueRanges) ReplacePlaceholdersSingle(
        ItemTemplate template, ItemTier tier, string text)
    {
        string result = text ?? "";
        var valueRanges = new List<(int Start, int Length)>();
        var matches = new List<(int Index, string Placeholder, string Key, bool AsSeconds)>();
        foreach (var (placeholder, key, asSeconds) in Placeholders)
        {
            int idx = 0;
            while ((idx = result.IndexOf(placeholder, idx, StringComparison.Ordinal)) >= 0)
            {
                matches.Add((idx, placeholder, key, asSeconds));
                idx += placeholder.Length;
            }
        }
        int offset = 0;
        foreach (var m in matches.OrderBy(x => x.Index))
        {
            int idx = m.Index + offset;
            string replaceStr = m.AsSeconds
                ? FormatCooldownSeconds(template.GetInt(m.Key, tier))
                : template.GetInt(m.Key, tier).ToString();
            result = result.Remove(idx, m.Placeholder.Length).Insert(idx, replaceStr);
            valueRanges.Add((idx, replaceStr.Length));
            offset += replaceStr.Length - m.Placeholder.Length;
        }
        return (result, valueRanges);
    }

    /// <summary>全 tier 替换：返回替换后的字符串及每个数值的（起始、长度、tier），用于分 tier 着色加粗。</summary>
    public static (string Result, List<(int Start, int Length, ItemTier Tier)> ValueRanges) ReplacePlaceholdersAllTiers(
        ItemTemplate template, string text)
    {
        string result = text ?? "";
        var valueRanges = new List<(int Start, int Length, ItemTier Tier)>();
        var snapshot = template.GetIntsByTierSnapshot();
        var matches = new List<(int Index, string Placeholder, string Key, bool AsSeconds)>();
        foreach (var (placeholder, key, asSeconds) in Placeholders)
        {
            int idx = 0;
            while ((idx = result.IndexOf(placeholder, idx, StringComparison.Ordinal)) >= 0)
            {
                matches.Add((idx, placeholder, key, asSeconds));
                idx += placeholder.Length;
            }
        }
        int offset = 0;
        foreach (var (index, placeholder, key, asSeconds) in matches.OrderBy(x => x.Index))
        {
            int idx = index + offset;
            if (!snapshot.TryGetValue(key, out var list) || list.Count == 0)
            {
                result = result.Remove(idx, placeholder.Length).Insert(idx, "0");
                valueRanges.Add((idx, 1, ItemTier.Bronze));
                offset += 1 - placeholder.Length;
                continue;
            }
            string replaceStr = string.Join(" » ", asSeconds
                ? list.Select(ms => FormatCooldownSeconds(ms))
                : list.Select(v => v.ToString()));
            result = result.Remove(idx, placeholder.Length).Insert(idx, replaceStr);
            int off = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) off += 3; // " » "
                string s = asSeconds ? FormatCooldownSeconds(list[i]) : list[i].ToString();
                valueRanges.Add((idx + off, s.Length, (ItemTier)i));
                off += s.Length;
            }
            offset += replaceStr.Length - placeholder.Length;
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
