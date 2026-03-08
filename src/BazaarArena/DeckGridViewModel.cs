using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using BazaarArena.Core;

namespace BazaarArena;

/// <summary>卡组表格中单个物品的显示项：起始列、跨列数、对应槽位行。</summary>
public class DeckSlotDisplayItem : INotifyPropertyChanged
{
    private int _startColumn;
    private int _columnSpan;
    private SlotRowViewModel _viewModel = null!;

    public int StartColumn
    {
        get => _startColumn;
        set { _startColumn = value; OnPropertyChanged(); }
    }

    public int ColumnSpan
    {
        get => _columnSpan;
        set { _columnSpan = value; OnPropertyChanged(); }
    }

    public SlotRowViewModel ViewModel
    {
        get => _viewModel;
        set { _viewModel = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>等级行中每一列单元格：列索引、是否可用、等级颜色画刷（null 表示空槽，Black 表示不可用列）。</summary>
public class TierRowCellViewModel : INotifyPropertyChanged
{
    private int _columnIndex;
    private bool _isAvailable;
    private Brush? _tierBrush;

    public int ColumnIndex
    {
        get => _columnIndex;
        set { _columnIndex = value; OnPropertyChanged(); }
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        set { _isAvailable = value; OnPropertyChanged(); }
    }

    public Brush? TierBrush
    {
        get => _tierBrush;
        set { _tierBrush = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
