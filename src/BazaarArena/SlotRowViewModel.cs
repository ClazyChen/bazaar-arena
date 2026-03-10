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

    /// <summary>局外复写的属性（键为属性名，值为数值）；与 <see cref="DeckSlotEntry.Overrides"/> 一致。</summary>
    private Dictionary<string, int>? _overrides;

    public Dictionary<string, int>? Overrides
    {
        get => _overrides;
        set { _overrides = value == null ? null : new Dictionary<string, int>(value); OnPropertyChanged(); }
    }

    /// <summary>设置单条复写并触发属性变更通知。</summary>
    public void SetOverride(string key, int value)
    {
        _overrides ??= new Dictionary<string, int>();
        _overrides[key] = value;
        OnPropertyChanged(nameof(Overrides));
    }

    /// <summary>移除单条复写。</summary>
    public void RemoveOverride(string key)
    {
        if (_overrides == null) return;
        _overrides.Remove(key);
        if (_overrides.Count == 0) _overrides = null;
        OnPropertyChanged(nameof(Overrides));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
