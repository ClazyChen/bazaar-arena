using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BazaarArena;

/// <summary>物品池中一项：基名（最新版本名）与当前显示名（可能为历史版本），用于多版本切换。</summary>
public class ItemPoolEntryViewModel : INotifyPropertyChanged
{
    private string _displayName;

    public ItemPoolEntryViewModel(string baseName, string displayName)
    {
        BaseName = baseName ?? "";
        _displayName = displayName ?? baseName ?? "";
    }

    /// <summary>当前行代表的“最新版本”名称，用于筛选与重置。</summary>
    public string BaseName { get; }

    /// <summary>当前展示的模板名（可能为历史版本），用于显示、ToolTip、拖拽。</summary>
    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (_displayName == value) return;
            _displayName = value ?? BaseName;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
