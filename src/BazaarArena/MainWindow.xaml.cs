using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
    /// <summary>物品池 ToolTip 缓存（按 itemName），仅在即将显示时构建并缓存，避免 MouseEnter 卡顿。</summary>
    private readonly Dictionary<string, Border> _poolToolTipCache = [];

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
        else
        {
            // 仅在没有 default.json 时生成空卡组集，避免覆盖用户测试用的文件
            try
            {
                var dir = Path.GetDirectoryName(defaultPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                _deckManager.NewCollection();
                _deckManager.SaveCollection(defaultPath);
                RefreshDeckList();
            }
            catch { /* 无法创建则保持空列表 */ }
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
                Overrides = entry.Overrides != null ? new Dictionary<string, int>(entry.Overrides) : null,
            });
        }
        UpdateSlotSummary();
        ShowEditor(true);
    }

    private void RenameDeck_Click(object sender, RoutedEventArgs e)
    {
        if (DeckListBox.SelectedItem is not DeckManager.DeckManager.DeckListItem item)
        {
            MessageBox.Show("请先选择要重命名的卡组。", "重命名", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new SaveAsDialog { Owner = this, InitialDeckId = item.Id };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.DeckId)) return;
        string newId = dlg.DeckId.Trim();
        if (newId == item.Id) return;
        try
        {
            _deckManager.Rename(item.Id, newId);
            if (_currentDeckId == item.Id)
                _currentDeckId = newId;
            RefreshDeckList();
            TrySaveCollectionToFile();
            DeckListBox.SelectedItem = _deckListItems.FirstOrDefault(x => x.Id == newId);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "重命名失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void MoveDeckUp_Click(object sender, RoutedEventArgs e)
    {
        if (DeckListBox.SelectedItem is not DeckManager.DeckManager.DeckListItem item) return;
        if (!_deckManager.MoveUp(item.Id)) return;
        RefreshDeckList();
        TrySaveCollectionToFile();
        DeckListBox.SelectedItem = _deckListItems.FirstOrDefault(x => x.Id == item.Id);
    }

    private void MoveDeckDown_Click(object sender, RoutedEventArgs e)
    {
        if (DeckListBox.SelectedItem is not DeckManager.DeckManager.DeckListItem item) return;
        if (!_deckManager.MoveDown(item.Id)) return;
        RefreshDeckList();
        TrySaveCollectionToFile();
        DeckListBox.SelectedItem = _deckListItems.FirstOrDefault(x => x.Id == item.Id);
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
            ItemTier.Bronze => new SolidColorBrush(Color.FromRgb(180, 98, 65)),
            ItemTier.Silver => new SolidColorBrush(Color.FromRgb(192, 192, 192)),
            ItemTier.Gold => new SolidColorBrush(Color.FromRgb(255, 215, 0)),
            ItemTier.Diamond => new SolidColorBrush(Color.FromRgb(0, 255, 255)),
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
        RebuildOverrideButtonRow();
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
            ApplyOverridableDefaultsForTier(row, next);
            RebuildTierRow();
            RebuildDeckNameRow();
        }
    }

    /// <summary>将模板的 OverridableAttributes 按指定 tier 的默认值写入 row.Overrides（仅更新这些键）。</summary>
    private void ApplyOverridableDefaultsForTier(SlotRowViewModel row, ItemTier tier)
    {
        var template = _itemDatabase.GetTemplate(row.ItemName);
        if (template?.OverridableAttributes == null) return;
        foreach (var kv in template.OverridableAttributes)
        {
            var list = kv.Value.ToList();
            int ti = (int)tier;
            int val = ti >= 0 && ti < list.Count ? list[ti] : (list.Count > 0 ? list[0] : 0);
            row.SetOverride(kv.Key, val);
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
            var template = _itemDatabase.GetTemplate(item.ViewModel.ItemName);
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
            var tt = new ToolTip();
            tt.Opened += DeckSlot_ToolTipOpened;
            border.ToolTip = tt;
            System.Windows.Controls.ToolTipService.SetInitialShowDelay(border, 400);
            Grid.SetColumn(border, item.StartColumn);
            Grid.SetColumnSpan(border, item.ColumnSpan);
            DeckImageRowGrid.Children.Add(border);
        }
    }

    private static readonly Brush ToolTipBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#35322e"));
    private static readonly Brush ToolTipForeground = Brushes.White;

    /// <summary>卡组内 ToolTip 即将显示时再构建内容，避免 MouseEnter 时卡顿。</summary>
    private void DeckSlot_ToolTipOpened(object sender, EventArgs e)
    {
        if (sender is not ToolTip tt || tt.PlacementTarget is not Border border || border.Tag is not SlotRowViewModel row) return;
        var template = _itemDatabase.GetTemplate(row.ItemName);
        if (template != null)
            tt.Content = BuildDeckSlotToolTip(template, row.Tier);
    }

    /// <summary>构建物品标签行：尺寸（小型/中型/大型）+ 现有 Tags，用空格连接。</summary>
    private static string BuildTagsLine(ItemTemplate template)
    {
        var parts = new List<string> { template.Size.GetDisplayName() };
        if (template.Tags?.Count > 0)
            parts.AddRange(template.Tags);
        return string.Join(" ", parts);
    }

    /// <summary>卡组内物品悬停：名称按 tier 色、冷却（若有）、Desc；占位符为单 tier 数值并加粗。</summary>
    private static Border BuildDeckSlotToolTip(ItemTemplate template, ItemTier tier)
    {
        var panel = new StackPanel { Margin = new Thickness(2) };
        var line1 = new TextBlock { Foreground = ToolTipForeground };
        line1.Inlines.Add(new Run(template.Name) { FontWeight = FontWeights.Bold, Foreground = TierToBrush(tier) });
        panel.Children.Add(line1);
        var tagsLine = new TextBlock { Foreground = ToolTipForeground, FontStyle = FontStyles.Italic };
        tagsLine.Inlines.Add(new Run(BuildTagsLine(template)));
        panel.Children.Add(tagsLine);
        if (template.GetInt("CooldownMs", tier) > 0)
        {
            var (line2, ranges2) = ItemDescHelper.ReplacePlaceholdersSingle(template, tier, "冷却时间：{Cooldown} 秒");
            var tb2 = new TextBlock { Foreground = ToolTipForeground };
            foreach (var inline in ItemDescHelper.BuildLineInlines(line2, ranges2, null))
                tb2.Inlines.Add(inline);
            panel.Children.Add(tb2);
        }
        if (!string.IsNullOrEmpty(template.Desc))
        {
            foreach (var segment in template.Desc.Split([';', '；']))
            {
                var trimmed = segment.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var (line3, ranges3) = ItemDescHelper.ReplacePlaceholdersSingle(template, tier, trimmed);
                var tb3 = new TextBlock { Foreground = ToolTipForeground };
                foreach (var inline in ItemDescHelper.BuildLineInlines(line3, ranges3, null))
                    tb3.Inlines.Add(inline);
                panel.Children.Add(tb3);
            }
        }
        var wrap = new Border
        {
            Background = ToolTipBackground,
            Child = panel,
            Padding = new Thickness(2, 2, 2, 2),
        };
        return wrap;
    }

    /// <summary>物品池悬停：名称、冷却（若有）、Desc；占位符为全 tier「5 » 10 » 15 » 20」并加粗按 tier 着色。</summary>
    private Border BuildPoolItemToolTip(string itemName)
    {
        var panel = new StackPanel { Margin = new Thickness(2) };
        var template = _itemDatabase.GetTemplate(itemName);
        if (template == null)
        {
            panel.Children.Add(new TextBlock { Text = itemName, FontWeight = FontWeights.Bold, Foreground = ToolTipForeground });
            return new Border { Background = ToolTipBackground, Child = panel, Padding = new Thickness(2, 2, 2, 2) };
        }
        var line1 = new TextBlock { Foreground = ToolTipForeground };
        line1.Inlines.Add(new Run(template.Name) { FontWeight = FontWeights.Bold, Foreground = ToolTipForeground });
        panel.Children.Add(line1);
        var tagsLine = new TextBlock { Foreground = ToolTipForeground, FontStyle = FontStyles.Italic };
        tagsLine.Inlines.Add(new Run(BuildTagsLine(template)));
        panel.Children.Add(tagsLine);
        bool hasCooldown = Enumerable.Range(0, 4).Any(i => template.GetInt("CooldownMs", (ItemTier)i) > 0);
        if (hasCooldown)
        {
            var (line2, ranges2) = ItemDescHelper.ReplacePlaceholdersAllTiers(template, "冷却时间：{Cooldown} 秒", ToolTipForeground);
            var tb2 = new TextBlock { Foreground = ToolTipForeground };
            var ranges2Simple = ranges2.Select(r => (r.Start, r.Length, r.OverrideBrush)).ToList();
            foreach (var inline in ItemDescHelper.BuildLineInlines(line2, ranges2Simple, null))
                tb2.Inlines.Add(inline);
            panel.Children.Add(tb2);
        }
        if (!string.IsNullOrEmpty(template.Desc))
        {
            foreach (var segment in template.Desc.Split([';', '；']))
            {
                var trimmed = segment.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var (line3, ranges3) = ItemDescHelper.ReplacePlaceholdersAllTiers(template, trimmed, ToolTipForeground);
                var tb3 = new TextBlock { Foreground = ToolTipForeground };
                foreach (var inline in ItemDescHelper.BuildLineInlinesWithTiers(line3, ranges3, TierToBrush))
                    tb3.Inlines.Add(inline);
                panel.Children.Add(tb3);
            }
        }
        return new Border { Background = ToolTipBackground, Child = panel, Padding = new Thickness(2, 2, 2, 2) };
    }

    /// <summary>物品池 ToolTip 即将显示时再构建（或从缓存取），避免 MouseEnter 卡顿。</summary>
    private void ItemPoolTile_ToolTipOpening(object sender, ToolTipEventArgs e)
    {
        if (sender is not FrameworkElement el || el.DataContext is not string itemName) return;
        if (el.ToolTip is not ToolTip tt) return;
        if (_poolToolTipCache.TryGetValue(itemName, out var cached))
        {
            tt.Content = cached;
            return;
        }
        var content = BuildPoolItemToolTip(itemName);
        _poolToolTipCache[itemName] = content;
        tt.Content = content;
    }

    private void RebuildDeckNameRow()
    {
        if (DeckNameRowGrid == null) return;
        DeckNameRowGrid.Children.Clear();
        const int colGap = 2;
        var cellMargin = new Thickness(colGap, 2, colGap, 0);
        foreach (var item in DeckSlotDisplays)
        {
            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = cellMargin,
            };
            stack.Children.Add(new TextBlock
            {
                Text = item.ViewModel.ItemName,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center,
            });
            if (item.ViewModel.Overrides != null)
            {
                foreach (var kv in item.ViewModel.Overrides)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = $"{kv.Key}: {kv.Value}",
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Colors.LightGray),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                    });
                }
            }
            Grid.SetColumn(stack, item.StartColumn);
            Grid.SetColumnSpan(stack, item.ColumnSpan);
            DeckNameRowGrid.Children.Add(stack);
        }
    }

    private void RebuildOverrideButtonRow()
    {
        if (DeckOverrideRowGrid == null) return;
        DeckOverrideRowGrid.Children.Clear();
        const int colGap = 2;
        var cellMargin = new Thickness(colGap, 2, colGap, 0);
        foreach (var item in DeckSlotDisplays)
        {
            var template = _itemDatabase.GetTemplate(item.ViewModel.ItemName);
            var btn = new Button
            {
                Content = "复写",
                Tag = item.ViewModel,
                Margin = cellMargin,
                Padding = new Thickness(6, 2, 6, 2),
                FontSize = 10,
                Visibility = template?.OverridableAttributes != null && template.OverridableAttributes.Count > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed,
            };
            btn.Click += OverrideButton_Click;
            Grid.SetColumn(btn, item.StartColumn);
            Grid.SetColumnSpan(btn, item.ColumnSpan);
            DeckOverrideRowGrid.Children.Add(btn);
        }
    }

    private void OverrideButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not SlotRowViewModel row) return;
        var template = _itemDatabase.GetTemplate(row.ItemName);
        if (template?.OverridableAttributes == null || template.OverridableAttributes.Count == 0) return;
        var dlg = new OverrideAttributeDialog(row, template, this) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            RebuildDeckNameRow();
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

    /// <summary>拖拽卡组内物品到编辑区非卡组区域（如物品池、空白处）时视为移除。</summary>
    private void EditorArea_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(SlotRowViewModelFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void EditorArea_Drop(object sender, DragEventArgs e)
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
        var newRow = new SlotRowViewModel { ItemNames = _itemNames, ItemName = itemName, Tier = tier };
        if (template.OverridableAttributes != null)
        {
            var overrides = new Dictionary<string, int>();
            foreach (var kv in template.OverridableAttributes)
            {
                var list = kv.Value.ToList();
                int ti = (int)tier;
                overrides[kv.Key] = ti >= 0 && ti < list.Count ? list[ti] : (list.Count > 0 ? list[0] : 0);
            }
            newRow.Overrides = overrides;
        }
        _slotRows.Insert(insertIndex, newRow);
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
            slots.Add(new DeckSlotEntry
            {
                ItemName = row.ItemName.Trim(),
                Tier = row.Tier,
                Overrides = row.Overrides != null ? new Dictionary<string, int>(row.Overrides) : null,
            });
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
