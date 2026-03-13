using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BazaarArena.Core;

namespace BazaarArena;

/// <summary>物品相关的通用 UI 辅助：包括 tier 颜色、标签行与 Tooltip 内容构建。</summary>
internal static class ItemUiHelper
{
    public static readonly Brush ToolTipBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#35322e"));
    public static readonly Brush ToolTipForeground = Brushes.White;

    /// <summary>根据物品档位返回对应的前景或背景画刷（与卡组管理器一致）。</summary>
    public static Brush TierToBrush(ItemTier tier)
    {
        return tier switch
        {
            ItemTier.Bronze => new SolidColorBrush(Color.FromRgb(180, 98, 65)),
            ItemTier.Silver => new SolidColorBrush(Color.FromRgb(192, 192, 192)),
            ItemTier.Gold => new SolidColorBrush(Color.FromRgb(255, 215, 0)),
            ItemTier.Diamond => new SolidColorBrush(Color.FromRgb(0, 255, 255)),
            _ => Brushes.White,
        };
    }

    /// <summary>构建物品标签行：尺寸（小型/中型/大型）+ 现有 Tags，用空格连接。</summary>
    public static string BuildTagsLine(ItemTemplate template)
    {
        // UI 只显示 1 次尺寸；并隐藏注册时自动补充的“尺寸/效果类型”标签（伤害/护盾/治疗等）。
        // 这些标签主要用于 Condition/可暴击判定等内部逻辑，展示出来会显得冗余且容易造成尺寸重复。
        var hiddenAutoTags = new HashSet<string>
        {
            Tag.Small,
            Tag.Medium,
            Tag.Large,
            Tag.Damage,
            Tag.Shield,
            Tag.Heal,
            Tag.Burn,
            Tag.Poison,
            Tag.Regen,
        };

        var parts = new List<string> { template.Size.GetDisplayName() };
        if (template.Tags?.Count > 0)
        {
            foreach (var tag in template.Tags)
            {
                if (hiddenAutoTags.Contains(tag)) continue;
                if (!parts.Contains(tag))
                    parts.Add(tag);
            }
        }
        return string.Join(" ", parts);
    }

    /// <summary>卡组/统计中单个物品的 Tooltip：名称按 tier 色、冷却（若有）、Desc；占位符为单 tier 数值并加粗。</summary>
    public static Border BuildDeckSlotToolTip(ItemTemplate template, ItemTier tier)
    {
        var panel = new StackPanel { Margin = new Thickness(2) };
        var line1 = new TextBlock { Foreground = ToolTipForeground };
        line1.Inlines.Add(new Run(template.Name) { FontWeight = FontWeights.Bold, Foreground = TierToBrush(tier) });
        panel.Children.Add(line1);
        var tagsLine = new TextBlock { Foreground = ToolTipForeground, FontStyle = FontStyles.Italic };
        tagsLine.Inlines.Add(new Run(BuildTagsLine(template)));
        panel.Children.Add(tagsLine);
        if (template.GetInt("CooldownMs", tier) > 0)
        {
            var (line2, ranges2) = ItemDescHelper.ReplacePlaceholdersSingle(template, tier, "冷却时间：{Cooldown} 秒");
            var tb2 = new TextBlock { Foreground = ToolTipForeground };
            foreach (var inline in ItemDescHelper.BuildLineInlines(line2, ranges2, null))
                tb2.Inlines.Add(inline);
            panel.Children.Add(tb2);
        }
        if (!string.IsNullOrEmpty(template.Desc))
        {
            foreach (var segment in template.Desc.Split([';', '；']))
            {
                var trimmed = segment.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var (line3, ranges3) = ItemDescHelper.ReplacePlaceholdersSingle(template, tier, trimmed);
                var tb3 = new TextBlock { Foreground = ToolTipForeground };
                foreach (var inline in ItemDescHelper.BuildLineInlines(line3, ranges3, null))
                    tb3.Inlines.Add(inline);
                panel.Children.Add(tb3);
            }
        }
        var wrap = new Border
        {
            Background = ToolTipBackground,
            Child = panel,
            Padding = new Thickness(2, 2, 2, 2),
        };
        return wrap;
    }
}

