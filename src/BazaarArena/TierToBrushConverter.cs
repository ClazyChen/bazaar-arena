using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using BazaarArena.Core;

namespace BazaarArena;

/// <summary>将物品等级转换为对应颜色画刷：铜 #CD7F32、银 #C0C0C0、金 #FFD700、钻 #B9F2FF。</summary>
public class TierToBrushConverter : IValueConverter
{
    private static readonly Dictionary<ItemTier, SolidColorBrush> Brushes = new()
    {
        [ItemTier.Bronze] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CD7F32")),
        [ItemTier.Silver] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0C0C0")),
        [ItemTier.Gold] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700")),
        [ItemTier.Diamond] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B9F2FF")),
    };

    static TierToBrushConverter()
    {
        foreach (var b in Brushes.Values) b.Freeze();
    }

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ItemTier tier && Brushes.TryGetValue(tier, out var brush))
            return brush;
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
