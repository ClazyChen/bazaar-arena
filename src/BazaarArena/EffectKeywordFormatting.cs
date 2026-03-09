using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace BazaarArena;

/// <summary>效果关键词着色：与战斗日志一致的「伤害」「灼烧」等颜色与加粗规则，供 ToolTip 与日志共用。</summary>
public static class EffectKeywordFormatting
{
    /// <summary>关键词与样式（按长度从长到短排序，避免「生命再生」被拆成「再生」）。</summary>
    private static readonly (string Keyword, Color Color, bool Bold)[] Rules =
    [
        ("生命再生", Color.FromRgb(0x27, 0xae, 0x60), false),
        ("灼烧结算", Color.FromRgb(255, 159, 69), false),
        ("剧毒结算", Color.FromRgb(14, 190, 79), false),
        ("沙尘暴", Color.FromRgb(0x95, 0xa5, 0xa6), false),
        ("暴击率", Color.FromRgb(0xf5, 0x50, 0x3d), false),
        ("暴击", Color.FromRgb(0xf5, 0x50, 0x3d), false),
        ("伤害", Color.FromRgb(0xf5, 0x50, 0x3d), false),
        ("灼烧", Color.FromRgb(255, 159, 69), false),
        ("剧毒", Color.FromRgb(14, 190, 79), false),
        ("护盾", Color.FromRgb(0x34, 0x98, 0xdb), false),
        ("生命值", Color.FromRgb(142, 234, 49), false),
        ("治疗", Color.FromRgb(142, 234, 49), false),
        ("回复", Color.FromRgb(142, 234, 49), false),
        ("受到", Color.FromRgb(0xe7, 0x4c, 0x3c), false),
        ("施放", Color.FromRgb(0x7f, 0x8c, 0x8d), false),
        ("结果", Color.FromRgb(0x7f, 0x8c, 0x8d), false),
    ];

    /// <summary>将一行文本按关键词拆成带颜色的 Run，返回 Inline 列表，供 TextBlock.Inlines 或 Paragraph 使用。</summary>
    public static List<Inline> BuildInlines(string line)
    {
        var list = new List<Inline>();
        if (string.IsNullOrEmpty(line))
        {
            list.Add(new Run(line ?? ""));
            return list;
        }

        int pos = 0;
        while (pos < line.Length)
        {
            int nextIndex = line.Length;
            (string keyword, Color color, bool bold)? matched = null;
            foreach (var (keyword, color, bold) in Rules)
            {
                int idx = line.IndexOf(keyword, pos, StringComparison.Ordinal);
                if (idx >= 0 && idx < nextIndex)
                {
                    nextIndex = idx;
                    matched = (keyword, color, bold);
                }
            }
            if (matched == null)
            {
                list.Add(new Run(line[pos..]));
                break;
            }
            if (nextIndex > pos)
                list.Add(new Run(line.Substring(pos, nextIndex - pos)));
            bool insideBrackets = IsInsideSquareBrackets(line, nextIndex);
            if (insideBrackets)
                list.Add(new Run(matched.Value.keyword));
            else
            {
                list.Add(new Run(matched.Value.keyword)
                {
                    Foreground = new SolidColorBrush(matched.Value.color),
                    FontWeight = matched.Value.bold ? FontWeights.Bold : FontWeights.Normal,
                });
            }
            pos = nextIndex + matched.Value.keyword.Length;
        }
        return list;
    }

    /// <summary>将一行日志按关键词拆成带颜色的 Run，组成一个 Paragraph（供 RichTextBox/FlowDocument 使用）。</summary>
    public static Paragraph BuildParagraph(string line)
    {
        var para = new Paragraph { Margin = new Thickness(0) };
        foreach (var inline in BuildInlines(line))
            para.Inlines.Add(inline);
        return para;
    }

    /// <summary>判断位置是否在方括号内（如物品名 [xxx]），在此处不应对关键词着色。</summary>
    public static bool IsInsideSquareBrackets(string line, int position)
    {
        int depth = 0;
        for (int i = 0; i < position; i++)
        {
            if (line[i] == '[') depth++;
            else if (line[i] == ']') depth--;
        }
        return depth > 0;
    }
}
