using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BazaarArena.Core;

namespace BazaarArena;

public partial class OverrideAttributeDialog
{
    private readonly SlotRowViewModel _row;
    private readonly ItemTemplate _template;

    public OverrideAttributeDialog(SlotRowViewModel row, ItemTemplate template, Window owner)
    {
        InitializeComponent();
        Owner = owner;
        Title = $"复写属性 - {template.Name}";
        _row = row;
        _template = template;
        Loaded += (_, _) =>
        {
            var keys = _template.OverridableAttributes!.Keys.ToList();
            AttributeCombo.ItemsSource = keys;
            if (keys.Count > 0)
            {
                AttributeCombo.SelectedIndex = 0;
                ValueBox.Focus();
            }
        };
    }

    private void AttributeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AttributeCombo.SelectedItem is not int key) return;
        string keyName = Key.GetName(key);
        int current = _row.Overrides != null && _row.Overrides.TryGetValue(keyName, out var v) ? v : GetDefaultForTier(key);
        ValueBox.Text = current.ToString();
    }

    private int GetDefaultForTier(int key)
    {
        if (!_template.OverridableAttributes!.TryGetValue(key, out var byTier)) return 0;
        var list = byTier.ToList();
        int ti = (int)_row.Tier;
        return ti >= 0 && ti < list.Count ? list[ti] : (list.Count > 0 ? list[0] : 0);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (AttributeCombo.SelectedItem is not int key)
        {
            MessageBox.Show("请选择要复写的属性。", "复写", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!int.TryParse(ValueBox.Text, out int value))
        {
            MessageBox.Show("请输入有效的整数。", "复写", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _row.SetOverride(Key.GetName(key), value);
        DialogResult = true;
        Close();
    }
}
