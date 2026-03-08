using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BazaarArena;

/// <summary>物品图像：约定为 pictures/png/&lt;Name&gt;.png，优先从输出目录查找。</summary>
public static class ItemImageHelper
{
    private static string? _picturesDir;

    /// <summary>获取 pictures/png 目录的完整路径（输出目录或当前目录下）。</summary>
    public static string GetPicturesPngDir()
    {
        if (_picturesDir != null) return _picturesDir;
        var baseDir = AppContext.BaseDirectory;
        var dir = Path.Combine(baseDir, "pictures", "png");
        if (Directory.Exists(dir))
        {
            _picturesDir = dir;
            return dir;
        }
        dir = Path.Combine(Directory.GetCurrentDirectory(), "pictures", "png");
        _picturesDir = Directory.Exists(dir) ? dir : Path.Combine(baseDir, "pictures", "png");
        return _picturesDir;
    }

    /// <summary>根据物品名称返回图像路径，不存在则返回 null。</summary>
    public static string? GetImagePath(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName)) return null;
        var dir = GetPicturesPngDir();
        var path = Path.Combine(dir, itemName.Trim() + ".png");
        return File.Exists(path) ? path : null;
    }

    /// <summary>根据物品名称加载为 ImageSource，不存在则返回 null（可显示占位）。</summary>
    public static ImageSource? GetImageSource(string itemName)
    {
        var path = GetImagePath(itemName);
        if (path == null) return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
