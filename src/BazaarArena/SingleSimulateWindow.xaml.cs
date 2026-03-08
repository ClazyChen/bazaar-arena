using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using BazaarArena.BattleSimulator;
using BazaarArena.Core;
using BazaarArena.DeckManager;
using BazaarArena.ItemDatabase;

using Simulator = BazaarArena.BattleSimulator.BattleSimulator;

namespace BazaarArena;

public partial class SingleSimulateWindow
{
    private readonly DeckManager.DeckManager _deckManager;
    private readonly ItemDatabase.ItemDatabase _itemDatabase;
    private readonly ObservableCollection<ItemStatRow> _statsRows = [];

    public SingleSimulateWindow()
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
        DeckACombo.Items.Add("(无)"); // 可选空
        DeckBCombo.Items.Add("(理想目标)");

        StatsGrid.ItemsSource = _statsRows;
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        Deck? deckA = null;
        if (DeckACombo.SelectedItem is string idA && idA != "(无)")
        {
            deckA = _deckManager.Load(idA);
            if (deckA == null)
            {
                MessageBox.Show($"无法加载卡组：{idA}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        Deck? deckB = null;
        if (IdealTargetCheck.IsChecked == true)
            deckB = Deck.CreateInfiniteHpDummyDeck();
        else if (DeckBCombo.SelectedItem is string idB && idB != "(无)" && idB != "(理想目标)")
        {
            deckB = _deckManager.Load(idB);
            if (deckB == null)
            {
                MessageBox.Show($"无法加载卡组：{idB}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        if (deckA == null || deckB == null)
        {
            MessageBox.Show("请选择玩家1 和 玩家2 的卡组（或勾选理想目标）。", "单次模拟", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        LogBox.Clear();
        ResultText.Text = "模拟中…";
        _statsRows.Clear();
        CurveCanvas.Children.Clear();

        void AppendLog(string line)
        {
            Dispatcher.Invoke(() =>
            {
                LogBox.AppendText(line + "\n");
                LogBox.ScrollToEnd();
            });
        }

        var statsSink = new StatsCollectingSink { CurveIntervalMs = 500 };
        var logSink = new TextBoxBattleLogSink(BattleLogLevel.Detailed, AppendLog);
        var composite = new CompositeBattleLogSink(statsSink, logSink);

        var dA = deckA;
        var dB = deckB;
        var runButton = (System.Windows.Controls.Button)sender;
        runButton.IsEnabled = false;

        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var sim = new Simulator();
                int winner = sim.Run(dA, dB, _itemDatabase, composite, BattleLogLevel.Detailed);
                var stats = statsSink.GetStats();

                Dispatcher.Invoke(() =>
                {
                    runButton.IsEnabled = true;
                    ResultText.Text = stats.IsDraw
                        ? $"平局，用时 {stats.DurationMs / 1000.0:F1} 秒"
                        : $"玩家 {stats.Winner + 1} 获胜，用时 {stats.DurationMs / 1000.0:F1} 秒";

                    foreach (var row in stats.ItemStats)
                        _statsRows.Add(row);

                    DrawCurve(stats);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    runButton.IsEnabled = true;
                    ResultText.Text = "模拟出错";
                    AppendLog("错误: " + ex.Message);
                });
            }
        });
    }

    private void DrawCurve(BattleRunStats stats)
    {
        const double margin = 30;
        double w = CurveCanvas.ActualWidth;
        double h = CurveCanvas.ActualHeight;
        if (w <= 0 || h <= 0) w = 400; h = 180;
        double plotW = w - 2 * margin;
        double plotH = h - 2 * margin;

        var pts0 = stats.StrengthCurveSide0;
        var pts1 = stats.StrengthCurveSide1;
        if (pts0.Count == 0 && pts1.Count == 0) return;

        int maxTime = Math.Max(pts0.Count > 0 ? pts0[^1].TimeMs : 0, pts1.Count > 0 ? pts1[^1].TimeMs : 0);
        int maxTotal = 0;
        foreach (var p in pts0) { if (p.Total > maxTotal) maxTotal = p.Total; }
        foreach (var p in pts1) { if (p.Total > maxTotal) maxTotal = p.Total; }
        if (maxTotal <= 0) maxTotal = 1;
        if (maxTime <= 0) maxTime = 1;

        var points0 = new PointCollection();
        foreach (var p in pts0)
            points0.Add(new System.Windows.Point(margin + (double)p.TimeMs / maxTime * plotW, margin + plotH - (double)p.Total / maxTotal * plotH));

        var points1 = new PointCollection();
        foreach (var p in pts1)
            points1.Add(new System.Windows.Point(margin + (double)p.TimeMs / maxTime * plotW, margin + plotH - (double)p.Total / maxTotal * plotH));

        var line0 = new Polyline
        {
            Points = points0,
            Stroke = new SolidColorBrush(Colors.DarkBlue),
            StrokeThickness = 2,
        };
        var line1 = new Polyline
        {
            Points = points1,
            Stroke = new SolidColorBrush(Colors.DarkRed),
            StrokeThickness = 2,
        };
        CurveCanvas.Children.Add(line0);
        CurveCanvas.Children.Add(line1);
    }
}
