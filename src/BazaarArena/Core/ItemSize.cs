namespace BazaarArena.Core;

/// <summary>物品尺寸，对应占用卡槽数。</summary>
public enum ItemSize
{
    Small = 1,
    Medium = 2,
    Large = 3,
}

/// <summary>ItemSize 显示名，用于 ToolTip 等。</summary>
public static class ItemSizeExtensions
{
    public static string GetDisplayName(this ItemSize size) => size switch
    {
        ItemSize.Small => "小型",
        ItemSize.Medium => "中型",
        ItemSize.Large => "大型",
        _ => "",
    };
}
