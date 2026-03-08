using System.ComponentModel;
using System.Runtime.CompilerServices;
using BazaarArena.Core;

namespace BazaarArena;

/// <summary>卡组槽位一行，用于编辑界面绑定。</summary>
public class SlotRowViewModel : INotifyPropertyChanged
{
    private string _itemName = "";
    private ItemTier _tier = ItemTier.Bronze;

    public static IReadOnlyList<ItemTier> Tiers { get; } =
        [ItemTier.Bronze, ItemTier.Silver, ItemTier.Gold, ItemTier.Diamond];

    public IReadOnlyList<ItemTier> TierList => Tiers;

    public IReadOnlyList<string>? ItemNames { get; set; }

    public string ItemName
    {
        get => _itemName;
        set { _itemName = value ?? ""; OnPropertyChanged(); }
    }

    public ItemTier Tier
    {
        get => _tier;
        set { _tier = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
