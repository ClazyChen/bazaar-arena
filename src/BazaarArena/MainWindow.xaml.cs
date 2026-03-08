using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using BazaarArena.Core;
using BazaarArena.DeckManager;
using BazaarArena.ItemDatabase;

namespace BazaarArena;

public partial class MainWindow
{
    private readonly DeckManager.DeckManager _deckManager;
    private readonly ItemDatabase.ItemDatabase _itemDatabase;
    private readonly ObservableCollection<string> _deckNames = [];
    private readonly ObservableCollection<SlotRowViewModel> _slotRows = [];
    private string? _currentDeckId;
    private IReadOnlyList<string> _itemNames = [];

    public MainWindow()
    {
        InitializeComponent();
        var app = (App)Application.Current;
        _deckManager = app.DeckManager;
        _itemDatabase = app.ItemDatabase;
        _itemNames = _itemDatabase.GetAllNames();

        DeckListBox.ItemsSource = _deckNames;
        SlotEntriesList.ItemsSource = _slotRows;

        for (int i = 1; i <= 20; i++)
            PlayerLevelCombo.Items.Add(i);
        PlayerLevelCombo.SelectedIndex = 4; // 5 级

        AddItemNameCombo.ItemsSource = _itemNames;
        AddTierCombo.ItemsSource = SlotRowViewModel.Tiers;
        AddTierCombo.SelectedIndex = 0;

        RefreshDeckList();
    }

    private void RefreshDeckList()
    {
        _deckNames.Clear();
        foreach (var id in _deckManager.List())
            _deckNames.Add(id);
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

    private void OpenDeck_Click(object sender, RoutedEventArgs e)
    {
        if (DeckListBox.SelectedItem is string id)
        {
            LoadDeckIntoEditor(id);
            return;
        }
        MessageBox.Show("请先在左侧列表中选择要打开的卡组。", "打开卡组", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeckListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeckListBox.SelectedItem is not string id) return;
        LoadDeckIntoEditor(id);
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
        if (DeckListBox.SelectedItem is not string id)
        {
            MessageBox.Show("请先选择要删除的卡组。", "删除", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show($"确定要删除卡组「{id}」吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        _deckManager.Delete(id);
        if (_currentDeckId == id)
        {
            _currentDeckId = null;
            _slotRows.Clear();
            ShowEditor(false);
        }
        RefreshDeckList();
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
    }

    private void AddSlot_Click(object sender, RoutedEventArgs e)
    {
        if (AddItemNameCombo.SelectedItem is not string name)
        {
            MessageBox.Show("请选择要添加的物品。", "添加物品", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var tier = AddTierCombo.SelectedItem is ItemTier t ? t : ItemTier.Bronze;
        int level = GetPlayerLevel();
        if (!Deck.TierAllowedForLevel(tier, level))
        {
            MessageBox.Show($"玩家等级 {level} 不可选择该物品等级。", "添加物品", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var template = _itemDatabase.GetTemplate(name);
        if (template == null) return;
        int maxSlots = Deck.MaxSlotsForLevel(level);
        int used = 0;
        foreach (var row in _slotRows)
        {
            if (string.IsNullOrEmpty(row.ItemName)) continue;
            var tpl = _itemDatabase.GetTemplate(row.ItemName);
            used += tpl != null ? (int)tpl.Size : 1;
        }
        if (used + (int)template.Size > maxSlots)
        {
            MessageBox.Show($"槽位不足，无法添加该物品（需要 {template.Size} 槽）。", "添加物品", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _slotRows.Add(new SlotRowViewModel { ItemNames = _itemNames, ItemName = name, Tier = tier });
        UpdateSlotSummary();
    }

    private void RemoveSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SlotRowViewModel row)
        {
            _slotRows.Remove(row);
            UpdateSlotSummary();
        }
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
            MessageBox.Show("保存成功。", "保存", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveAsDeck_Click(object sender, RoutedEventArgs e)
    {
        var input = new SaveAsDialog();
        if (input.ShowDialog() != true || string.IsNullOrWhiteSpace(input.DeckId))
            return;
        string id = input.DeckId.Trim();
        try
        {
            var deck = BuildDeckFromEditor();
            _deckManager.Save(deck, id, _itemDatabase);
            _currentDeckId = id;
            RefreshDeckList();
            DeckListBox.SelectedItem = id;
            MessageBox.Show("另存为成功。", "保存", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
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

    private void DeckCompare_Click(object sender, RoutedEventArgs e)
    {
        var win = new BatchSimulateWindow { Owner = this, Title = "卡组互评" };
        win.ShowDialog();
    }
}
