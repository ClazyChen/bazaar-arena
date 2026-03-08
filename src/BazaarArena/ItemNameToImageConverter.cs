using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BazaarArena;

/// <summary>将物品名称转换为 ImageSource（pictures/png/&lt;Name&gt;.png），无图时返回 null。</summary>
public class ItemNameToImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string name)
            return ItemImageHelper.GetImageSource(name);
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
