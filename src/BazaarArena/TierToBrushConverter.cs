using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using BazaarArena.Core;

namespace BazaarArena;

/// <summary>将物品等级转换为对应颜色画刷：铜 bronze(180,98,65)、银 silver(192,192,192)、金 gold(255,215,0)、钻 diamond(0,255,255)。</summary>
public class TierToBrushConverter : IValueConverter
{
    private static readonly Dictionary<ItemTier, SolidColorBrush> Brushes = new()
    {
        [ItemTier.Bronze] = new SolidColorBrush(Color.FromRgb(180, 98, 65)),
        [ItemTier.Silver] = new SolidColorBrush(Color.FromRgb(192, 192, 192)),
        [ItemTier.Gold] = new SolidColorBrush(Color.FromRgb(255, 215, 0)),
        [ItemTier.Diamond] = new SolidColorBrush(Color.FromRgb(0, 255, 255)),
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
