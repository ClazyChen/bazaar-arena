using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BazaarArena.Core;
using BazaarArena.DeckManager;
using BazaarArena.ItemDatabase;
using Microsoft.Win32;

namespace BazaarArena;

public partial class MainWindow
{
    private readonly DeckManager.DeckManager _deckManager;
    private readonly ItemDatabase.ItemDatabase _itemDatabase;
    private readonly ObservableCollection<DeckManager.DeckManager.DeckListItem> _deckListItems = [];
    private readonly ObservableCollection<SlotRowViewModel> _slotRows = [];
    private string? _currentDeckId;
    private IReadOnlyList<string> _itemNames = [];

    /// <summary>卡组表格第二行中每个物品的显示项（起始列、跨列、视图模型）。</summary>
    public ObservableCollection<DeckSlotDisplayItem> DeckSlotDisplays { get; } = [];

    private static readonly Brush UnavailableColumnBrush = Brushes.Black;
    private static readonly Brush EmptySlotBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
    private const string SlotRowViewModelFormat = "BazaarArena.SlotRowViewModel";

    public MainWindow()
    {
        InitializeComponent();
        var app = (App)Application.Current;
        _deckManager = app.DeckManager;
        _itemDatabase = app.ItemDatabase;
        _itemNames = _itemDatabase.GetAllNames();

        DeckListBox.ItemsSource = _deckListItems;

        for (int i = 1; i <= 20; i++)
            PlayerLevelCombo.Items.Add(i);
        PlayerLevelCombo.SelectedIndex = 4; // 5 级

        ItemPoolList.ItemsSource = _itemNames;

        RefreshDeckList();
        var defaultPath = Path.Combine(App.DecksDirectory, "default.json");
        if (File.Exists(defaultPath))
        {
            try
            {
                _deckManager.OpenCollection(defaultPath);
                RefreshDeckList();
            }
            catch { /* 启动时加载失败则保持空列表 */ }
        }
        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        var path = _deckManager.CurrentCollectionPath;
        Title = string.IsNullOrEmpty(path) ? "Bazaar Arena - 未命名" : $"Bazaar Arena - {Path.GetFileName(path)}";
    }

    private void DeckGridInner_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DeckGridInner is not { } grid || grid.ActualWidth <= 0) return;
        // small 物品 1:2（宽:高），单列宽 = ActualWidth/10，高 = 2*单列宽 = ActualWidth/5
        double h = Math.Max(40, grid.ActualWidth / 5);
        grid.RowDefinitions[1].Height = new GridLength(h);
    }

    private void RefreshDeckList()
    {
        _deckListItems.Clear();
        foreach (var item in _deckManager.ListWithLevels())
            _deckListItems.Add(item);
    }

    private void ShowEditor(bool show)
    {
        WelcomeText.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        EditorPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NewDeck_Click(object sender, RoutedEventArgs e)
    {
        _currentDeckId = null;
        _slotRows.Clear();
        PlayerLevelCombo.SelectedIndex = 4;
        UpdateSlotSummary();
        ShowEditor(true);
        DeckListBox.SelectedItem = null;
    }

    private void NewCollection_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "卡组集 JSON|*.json|所有文件|*.*",
            DefaultExt = ".json",
            Title = "新建卡组集",
            InitialDirectory = App.DecksDirectory
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _deckManager.NewCollection();
            _deckManager.SaveCollection(dlg.FileName);
            RefreshDeckList();
            _currentDeckId = null;
            _slotRows.Clear();
            ShowEditor(false);
            DeckListBox.SelectedItem = null;
            UpdateWindowTitle();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"创建失败：{ex.Message}", "新建卡组集", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenCollection_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "卡组集 JSON|*.json|所有文件|*.*",
            DefaultExt = ".json",
            Title = "打开卡组集",
            InitialDirectory = App.DecksDirectory
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _deckManager.OpenCollection(dlg.FileName);
            RefreshDeckList();
            _currentDeckId = null;
            _slotRows.Clear();
            ShowEditor(false);
            DeckListBox.SelectedItem = null;
            UpdateWindowTitle();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开文件：{ex.Message}", "打开卡组集", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DeckListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeckListBox.SelectedItem is not DeckManager.DeckManager.DeckListItem item) return;
        LoadDeckIntoEditor(item.Id);
    }

    private void LoadDeckIntoEditor(string id)
    {
        var deck = _deckManager.Load(id);
        if (deck == null)
        {
            MessageBox.Show($"无法加载卡组：{id}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _currentDeckId = id;
        PlayerLevelCombo.SelectedIndex = Math.Clamp(deck.PlayerLevel - 1, 0, 19);
        _slotRows.Clear();
        foreach (var entry in deck.Slots)
        {
            _slotRows.Add(new SlotRowViewModel
            {
                ItemNames = _itemNames,
                ItemName = entry.ItemName,
                Tier = entry.Tier,
            });
        }
        UpdateSlotSummary();
        ShowEditor(true);
    }

    private void DeleteDeck_Click(object sender, RoutedEventArgs e)
    {
        if (DeckListBox.SelectedItem is not DeckManager.DeckManager.DeckListItem item)
        {
            MessageBox.Show("请先选择要删除的卡组。", "删除", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        string id = item.Id;
        if (MessageBox.Show($"确定要删除卡组「{item.Display}」吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        _deckManager.Delete(id);
        if (_currentDeckId == id)
        {
            _currentDeckId = null;
            _slotRows.Clear();
            ShowEditor(false);
        }
        RefreshDeckList();
        TrySaveCollectionToFile();
    }

    private void PlayerLevel_Changed(object sender, SelectionChangedEventArgs e)
    {
        UpdateSlotSummary();
    }

    private int GetPlayerLevel()
    {
        if (PlayerLevelCombo.SelectedItem is int l) return l;
        return 5;
    }

    private void UpdateSlotSummary()
    {
        int level = PlayerLevelCombo.SelectedIndex >= 0 ? PlayerLevelCombo.SelectedIndex + 1 : 5;
        int maxSlots = Deck.MaxSlotsForLevel(level);
        int used = 0;
        foreach (var row in _slotRows)
        {
            if (string.IsNullOrEmpty(row.ItemName)) continue;
            var t = _itemDatabase.GetTemplate(row.ItemName);
            used += t != null ? (int)t.Size : 1;
        }
        SlotSummaryText.Text = $"槽位：已用 {used} / 上限 {maxSlots}";
        UpdateDeckGridDisplay();
    }

    private static Brush TierToBrush(ItemTier tier)
    {
        return tier switch
        {
            ItemTier.Bronze => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CD7F32")),
            ItemTier.Silver => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0C0C0")),
            ItemTier.Gold => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700")),
            ItemTier.Diamond => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B9F2FF")),
            _ => EmptySlotBrush,
        };
    }

    private void UpdateDeckGridDisplay()
    {
        int level = GetPlayerLevel();
        int maxSlots = Deck.MaxSlotsForLevel(level);
        int firstSlot = (10 - maxSlots) / 2;

        // 更新第二行物品显示（先算 DeckSlotDisplays，tier 行依赖它）
        DeckSlotDisplays.Clear();
        int currentCol = firstSlot;
        foreach (var row in _slotRows)
        {
            if (string.IsNullOrEmpty(row.ItemName)) continue;
            var t = _itemDatabase.GetTemplate(row.ItemName);
            int size = t != null ? (int)t.Size : 1;
            if (currentCol + size > firstSlot + maxSlots) break;
            DeckSlotDisplays.Add(new DeckSlotDisplayItem
            {
                StartColumn = currentCol,
                ColumnSpan = size,
                ViewModel = row,
            });
            currentCol += size;
        }

        RebuildTierRow();
        RebuildDeckImageRow();
        RebuildDeckNameRow();
    }

    private void RebuildTierRow()
    {
        if (TierRowGrid == null) return;
        TierRowGrid.Children.Clear();
        int level = GetPlayerLevel();
        int maxSlots = Deck.MaxSlotsForLevel(level);
        int firstSlot = (10 - maxSlots) / 2;

        const int colGap = 2; // 列间缝隙（每侧 2px，两列之间共 4px）
        var rowMargin = new Thickness(colGap, 1, colGap, 1);

        // 不可用列：左侧黑块
        if (firstSlot > 0)
        {
            var left = new Border { Background = UnavailableColumnBrush, Margin = rowMargin };
            Grid.SetColumn(left, 0);
            Grid.SetColumnSpan(left, firstSlot);
            TierRowGrid.Children.Add(left);
        }
        // 不可用列：右侧黑块
        int rightStart = firstSlot + maxSlots;
        if (rightStart < 10)
        {
            var right = new Border { Background = UnavailableColumnBrush, Margin = rowMargin };
            Grid.SetColumn(right, rightStart);
            Grid.SetColumnSpan(right, 10 - rightStart);
            TierRowGrid.Children.Add(right);
        }
        // 空槽位（可用但无物品）：按连续区间添加灰块
        int col = firstSlot;
        foreach (var item in DeckSlotDisplays)
        {
            if (col < item.StartColumn)
            {
                var empty = new Border { Background = EmptySlotBrush, Margin = rowMargin };
                Grid.SetColumn(empty, col);
                Grid.SetColumnSpan(empty, item.StartColumn - col);
                TierRowGrid.Children.Add(empty);
            }
            col = item.StartColumn + item.ColumnSpan;
        }
        if (col < rightStart)
        {
            var empty = new Border { Background = EmptySlotBrush, Margin = rowMargin };
            Grid.SetColumn(empty, col);
            Grid.SetColumnSpan(empty, rightStart - col);
            TierRowGrid.Children.Add(empty);
        }
        // 物品 tier 色块（与物品同跨列），可点击切换等级
        foreach (var item in DeckSlotDisplays)
        {
            var border = new Border
            {
                Background = TierToBrush(item.ViewModel.Tier),
                Tag = item.ViewModel,
                Cursor = Cursors.Hand,
                CornerRadius = new CornerRadius(1),
                Margin = rowMargin,
            };
            border.PreviewMouseLeftButtonDown += TierBlock_PreviewMouseLeftButtonDown;
            Grid.SetColumn(border, item.StartColumn);
            Grid.SetColumnSpan(border, item.ColumnSpan);
            TierRowGrid.Children.Add(border);
        }
    }

    private void TierBlock_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not SlotRowViewModel row) return;
        int level = GetPlayerLevel();
        ItemTier next = CycleToNextAllowedTier(row.Tier, level);
        if (next != row.Tier)
        {
            row.Tier = next;
            RebuildTierRow();
        }
    }

    private static ItemTier CycleToNextAllowedTier(ItemTier current, int playerLevel)
    {
        var order = new[] { ItemTier.Bronze, ItemTier.Silver, ItemTier.Gold, ItemTier.Diamond };
        int i = Array.IndexOf(order, current);
        for (int k = 1; k <= 4; k++)
        {
            ItemTier candidate = order[(i + k) % 4];
            if (Deck.TierAllowedForLevel(candidate, playerLevel))
                return candidate;
        }
        return current;
    }

    private void RebuildDeckImageRow()
    {
        if (DeckImageRowGrid == null) return;
        DeckImageRowGrid.Children.Clear();
        const int colGap = 2; // 与 tier 行一致，列间缝隙
        var cellMargin = new Thickness(colGap, 0, colGap, 0);
        foreach (var item in DeckSlotDisplays)
        {
            var border = new Border
            {
                Margin = cellMargin,
                Background = new SolidColorBrush(Colors.Transparent),
                Tag = item.ViewModel,
                Cursor = Cursors.Hand,
                Child = new Image
                {
                    Source = ItemImageHelper.GetImageSource(item.ViewModel.ItemName),
                    Stretch = Stretch.Fill,
                },
            };
            border.PreviewMouseLeftButtonDown += DeckItem_PreviewMouseLeftButtonDown;
            Grid.SetColumn(border, item.StartColumn);
            Grid.SetColumnSpan(border, item.ColumnSpan);
            DeckImageRowGrid.Children.Add(border);
        }
    }

    private void RebuildDeckNameRow()
    {
        if (DeckNameRowGrid == null) return;
        DeckNameRowGrid.Children.Clear();
        const int colGap = 2;
        var cellMargin = new Thickness(colGap, 2, colGap, 0);
        foreach (var item in DeckSlotDisplays)
        {
            var text = new TextBlock
            {
                Text = item.ViewModel.ItemName,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = item.ColumnSpan > 1 ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = cellMargin,
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.White),
            };
            Grid.SetColumn(text, item.StartColumn);
            Grid.SetColumnSpan(text, item.ColumnSpan);
            DeckNameRowGrid.Children.Add(text);
        }
    }

    private void DeckItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not SlotRowViewModel row) return;
        var data = new DataObject(SlotRowViewModelFormat, row);
        DragDrop.DoDragDrop(border, data, DragDropEffects.Move);
    }

    private void ItemPoolTile_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement el || el.DataContext is not string itemName) return;
        var data = new DataObject(DataFormats.Text, itemName);
        DragDrop.DoDragDrop(el, data, DragDropEffects.Copy);
    }

    private void DeleteZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(SlotRowViewModelFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void DeleteZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(SlotRowViewModelFormat)) return;
        if (e.Data.GetData(SlotRowViewModelFormat) is SlotRowViewModel row)
        {
            RemoveSlotRow(row);
            e.Handled = true;
        }
    }

    private void DeckGrid_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(SlotRowViewModelFormat) || e.Data.GetDataPresent(DataFormats.Text))
            e.Effects = e.Data.GetDataPresent(SlotRowViewModelFormat) ? DragDropEffects.Move : DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void DeckGrid_Drop(object sender, DragEventArgs e)
    {
        if (DeckImageRowGrid == null) return;
        var pos = e.GetPosition(DeckImageRowGrid);
        double w = DeckImageRowGrid.ActualWidth;
        if (w <= 0) return;
        int col = Math.Clamp((int)(pos.X / (w / 10)), 0, 9);
        int level = GetPlayerLevel();
        int maxSlots = Deck.MaxSlotsForLevel(level);
        int firstSlot = (10 - maxSlots) / 2;
        if (col < firstSlot || col >= firstSlot + maxSlots) return;
        int logicalSlot = col - firstSlot;

        if (e.Data.GetDataPresent(SlotRowViewModelFormat) && e.Data.GetData(SlotRowViewModelFormat) is SlotRowViewModel row)
        {
            MoveSlotToLogicalIndex(row, logicalSlot);
            e.Handled = true;
            return;
        }
        if (e.Data.GetDataPresent(DataFormats.Text) && e.Data.GetData(DataFormats.Text) is string itemName)
        {
            TryInsertItemAt(itemName, ItemTier.Bronze, logicalSlot);
            e.Handled = true;
        }
    }

    /// <summary>尝试将物品加入卡组（如槽位或等级不允许则返回 false）。</summary>
    public bool TryAddItemToDeck(string itemName, ItemTier tier = ItemTier.Bronze)
    {
        int maxSlots = Deck.MaxSlotsForLevel(GetPlayerLevel());
        return TryInsertItemAt(itemName, tier, maxSlots);
    }

    /// <summary>尝试在指定逻辑槽位插入物品（0 表示最左）。槽位不足或等级不允许时返回 false。</summary>
    public bool TryInsertItemAt(string itemName, ItemTier tier, int logicalSlot)
    {
        int level = GetPlayerLevel();
        if (!Deck.TierAllowedForLevel(tier, level))
            return false;
        var template = _itemDatabase.GetTemplate(itemName);
        if (template == null) return false;
        int maxSlots = Deck.MaxSlotsForLevel(level);
        int firstSlot = (10 - maxSlots) / 2;
        if (logicalSlot < 0 || logicalSlot > maxSlots) return false;
        int insertCol = firstSlot + logicalSlot;
        int used = 0;
        int insertIndex = 0;
        int col = firstSlot;
        foreach (var row in _slotRows)
        {
            if (string.IsNullOrEmpty(row.ItemName)) continue;
            var tpl = _itemDatabase.GetTemplate(row.ItemName);
            int size = tpl != null ? (int)tpl.Size : 1;
            if (col >= insertCol) break;
            col += size;
            used += size;
            insertIndex++;
        }
        if (used + (int)template.Size > maxSlots)
            return false;
        _slotRows.Insert(insertIndex, new SlotRowViewModel { ItemNames = _itemNames, ItemName = itemName, Tier = tier });
        UpdateSlotSummary();
        return true;
    }

    /// <summary>从卡组中移除指定槽位行。</summary>
    public void RemoveSlotRow(SlotRowViewModel row)
    {
        _slotRows.Remove(row);
        UpdateSlotSummary();
    }

    /// <summary>在卡组内移动物品：将 source 移到 targetLogicalSlot（0 到 maxSlots-1 的逻辑槽位）。</summary>
    public void MoveSlotToLogicalIndex(SlotRowViewModel source, int targetLogicalSlot)
    {
        int maxSlots = Deck.MaxSlotsForLevel(GetPlayerLevel());
        int firstSlot = (10 - maxSlots) / 2;
        if (targetLogicalSlot < 0 || targetLogicalSlot >= maxSlots) return;
        int targetCol = firstSlot + targetLogicalSlot;
        _slotRows.Remove(source);
        int insertIndex = 0;
        int col = firstSlot;
        foreach (var row in _slotRows)
        {
            if (string.IsNullOrEmpty(row.ItemName)) continue;
            var t = _itemDatabase.GetTemplate(row.ItemName);
            int size = t != null ? (int)t.Size : 1;
            if (col + size > targetCol) break;
            col += size;
            insertIndex++;
        }
        _slotRows.Insert(insertIndex, source);
        UpdateSlotSummary();
    }

    private Deck BuildDeckFromEditor()
    {
        int level = GetPlayerLevel();
        var slots = new List<DeckSlotEntry>();
        foreach (var row in _slotRows)
        {
            if (string.IsNullOrWhiteSpace(row.ItemName)) continue;
            slots.Add(new DeckSlotEntry { ItemName = row.ItemName.Trim(), Tier = row.Tier });
        }
        return new Deck { PlayerLevel = level, Slots = slots };
    }

    private void SaveDeck_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentDeckId))
        {
            SaveAsDeck_Click(sender, e);
            return;
        }
        try
        {
            var deck = BuildDeckFromEditor();
            _deckManager.Save(deck, _currentDeckId, _itemDatabase);
            RefreshDeckList();
            TrySaveCollectionToFile();
            MessageBox.Show("保存成功。", "保存", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveAsDeck_Click(object sender, RoutedEventArgs e)
    {
        var input = new SaveAsDialog { Owner = this };
        if (input.ShowDialog() != true || string.IsNullOrWhiteSpace(input.DeckId))
            return;
        string id = input.DeckId.Trim();
        try
        {
            var deck = BuildDeckFromEditor();
            _deckManager.Save(deck, id, _itemDatabase);
            _currentDeckId = id;
            RefreshDeckList();
            DeckListBox.SelectedItem = _deckListItems.FirstOrDefault(x => x.Id == id);
            TrySaveCollectionToFile();
            MessageBox.Show("另存为成功。", "保存", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>若已有关联文件则保存卡组集到该文件；否则（新建卡组集）弹出另存为对话框。</summary>
    private void TrySaveCollectionToFile()
    {
        if (_deckManager.SaveCollection())
        {
            UpdateWindowTitle();
            return;
        }
        var dlg = new SaveFileDialog
        {
            Filter = "卡组集 JSON|*.json|所有文件|*.*",
            DefaultExt = ".json",
            Title = "保存卡组集",
            InitialDirectory = App.DecksDirectory
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            _deckManager.SaveCollection(dlg.FileName);
            UpdateWindowTitle();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存卡组集失败：{ex.Message}", "保存", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void SingleSimulate_Click(object sender, RoutedEventArgs e)
    {
        var win = new SingleSimulateWindow { Owner = this };
        win.ShowDialog();
    }

    private void BatchSimulate_Click(object sender, RoutedEventArgs e)
    {
        var win = new BatchSimulateWindow { Owner = this };
        win.ShowDialog();
    }

}
