using System.Windows;
using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.DeckManager;
using BazaarArena.ItemDatabase;

namespace BazaarArena;

public partial class BatchSimulateWindow
{
    private readonly DeckManager.DeckManager _deckManager;
    private readonly ItemDatabase.ItemDatabase _itemDatabase;

    public BatchSimulateWindow()
    {
        InitializeComponent();
        var app = (App)Application.Current;
        _deckManager = app.DeckManager;
        _itemDatabase = app.ItemDatabase;

        foreach (var id in _deckManager.List())
        {
            DeckACombo.Items.Add(id);
            DeckBCombo.Items.Add(id);
        }
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        if (DeckACombo.SelectedItem is not string idA)
        {
            MessageBox.Show("请选择玩家1 的卡组。", "批量模拟", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (DeckBCombo.SelectedItem is not string idB)
        {
            MessageBox.Show("请选择玩家2 的卡组。", "批量模拟", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!int.TryParse(RunsBox.Text, out int n) || n < 1 || n > 100000)
        {
            MessageBox.Show("请输入有效场次（1–100000）。", "批量模拟", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var deckA = _deckManager.Load(idA);
        var deckB = _deckManager.Load(idB);
        if (deckA == null || deckB == null)
        {
            MessageBox.Show("无法加载所选卡组。", "批量模拟", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ProgressText.Text = "模拟中…";
        ProgressBar.Value = 0;
        ResultText.Text = "";
        var runButton = (System.Windows.Controls.Button)sender;
        runButton.IsEnabled = false;

        var silentSink = new SilentBattleLogSink();

        System.Threading.Tasks.Task.Run(() =>
        {
            int win0 = 0, win1 = 0, draw = 0;
            var sim = new BazaarArena.BattleSimulator.BattleSimulator();

            for (int i = 0; i < n; i++)
            {
                int w = sim.Run(deckA, deckB, _itemDatabase, silentSink, BattleLogLevel.None);
                if (w < 0) draw++;
                else if (w == 0) win0++;
                else win1++;

                if ((i + 1) % 10 == 0 || i == n - 1)
                {
                    double p = (double)(i + 1) / n;
                    Dispatcher.Invoke(() =>
                    {
                        ProgressBar.Value = p;
                        ProgressText.Text = $"已运行 {i + 1} / {n} 场";
                    });
                }
            }

            double p0 = 100.0 * win0 / n, p1 = 100.0 * win1 / n, pd = 100.0 * draw / n;
            Dispatcher.Invoke(() =>
            {
                runButton.IsEnabled = true;
                ProgressText.Text = $"共 {n} 场";
                ResultText.Text = $"玩家1（{idA}）胜率：{p0:F1}%  |  玩家2（{idB}）胜率：{p1:F1}%  |  平局：{pd:F1}%";
            });
        });
    }
}
