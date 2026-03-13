using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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

        foreach (var item in _deckManager.ListWithLevels())
        {
            DeckACombo.Items.Add(item);
            DeckBCombo.Items.Add(item);
        }
        if (DeckACombo.Items.Count > 0)
            DeckACombo.SelectedIndex = 0;
        if (DeckBCombo.Items.Count > 0)
            DeckBCombo.SelectedIndex = 0;

        StatsGrid.ItemsSource = _statsRows;

        // PageWidth=NaN 表示按视口宽度排版，长行自动换行且不出现横向滚动条
        LogBox.Document.PageWidth = double.NaN;
    }

    private void LogBox_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (e.Delta == 0 || LogScrollViewer.ScrollableHeight <= 0) return;
        var offset = LogScrollViewer.VerticalOffset - e.Delta;
        offset = Math.Max(0, Math.Min(LogScrollViewer.ScrollableHeight, offset));
        LogScrollViewer.ScrollToVerticalOffset(offset);
        e.Handled = true;
    }

    private void StatsGrid_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (e.Delta == 0 || StatsScrollViewer.ScrollableHeight <= 0) return;
        var offset = StatsScrollViewer.VerticalOffset - e.Delta;
        offset = Math.Max(0, Math.Min(StatsScrollViewer.ScrollableHeight, offset));
        StatsScrollViewer.ScrollToVerticalOffset(offset);
        e.Handled = true;
    }

    /// <summary>选中某一物品统计行时，在下方展示该物品的详细效果说明（与卡组管理界面的 Tooltip 一致）。</summary>
    private void StatsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedItemToolTipHost == null) return;
        SelectedItemToolTipHost.Child = null;
        if (StatsGrid.SelectedItem is not ItemStatRow row) return;
        if (string.IsNullOrEmpty(row.ItemName)) return;
        var template = _itemDatabase.GetTemplate(row.ItemName);
        if (template == null) return;
        var content = ItemUiHelper.BuildDeckSlotToolTip(template, row.Tier);
        SelectedItemToolTipHost.Child = content;
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        Deck? deckA = null;
        if (DeckACombo.SelectedItem is DeckManager.DeckManager.DeckListItem itemA)
        {
            deckA = _deckManager.Load(itemA.Id);
            if (deckA == null)
            {
                MessageBox.Show($"无法加载卡组：{itemA.Display}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        Deck? deckB = null;
        if (DeckBCombo.SelectedItem is DeckManager.DeckManager.DeckListItem itemB)
        {
            deckB = _deckManager.Load(itemB.Id);
            if (deckB == null)
            {
                MessageBox.Show($"无法加载卡组：{itemB.Display}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        if (deckA == null)
        {
            MessageBox.Show("请选择玩家1 的卡组。", "单次模拟", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (deckB == null)
        {
            MessageBox.Show("请选择玩家2 的卡组。", "单次模拟", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        LogBox.Document.Blocks.Clear();
        ResultText.Text = "模拟中…";
        _statsRows.Clear();
        CurveCanvas.Children.Clear();

        void AppendLog(string line)
        {
            Dispatcher.Invoke(() =>
            {
                var para = EffectKeywordFormatting.BuildParagraph(line);
                LogBox.Document.Blocks.Add(para);
                LogScrollViewer.ScrollToBottom();
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
        const double leftMargin = 44;
        const double rightMargin = 12;
        const double topMargin = 8;
        const double bottomMargin = 28;
        double w = CurveCanvas.ActualWidth;
        double h = CurveCanvas.ActualHeight;
        if (w <= 0 || h <= 0) { w = 400; h = 180; }
        double plotW = w - leftMargin - rightMargin;
        double plotH = h - topMargin - bottomMargin;

        var pts0 = stats.StrengthCurveSide0;
        var pts1 = stats.StrengthCurveSide1;
        if (pts0.Count == 0 && pts1.Count == 0) return;

        int maxTime = Math.Max(pts0.Count > 0 ? pts0[^1].TimeMs : 0, pts1.Count > 0 ? pts1[^1].TimeMs : 0);
        int maxHp = 1;
        foreach (var p in pts0) { if (p.Hp > maxHp) maxHp = p.Hp; }
        foreach (var p in pts1) { if (p.Hp > maxHp) maxHp = p.Hp; }
        if (maxTime <= 0) maxTime = 1;

        var points0 = new PointCollection();
        foreach (var p in pts0)
            points0.Add(new System.Windows.Point(leftMargin + (double)p.TimeMs / maxTime * plotW, topMargin + plotH - (double)p.Hp / maxHp * plotH));
        var points1 = new PointCollection();
        foreach (var p in pts1)
            points1.Add(new System.Windows.Point(leftMargin + (double)p.TimeMs / maxTime * plotW, topMargin + plotH - (double)p.Hp / maxHp * plotH));
        var color0 = Color.FromRgb(0x25, 0x63, 0xEB);  // 蓝色
        var color1 = Color.FromRgb(0xDC, 0x26, 0x26);  // 红色
        var line0 = new Polyline
        {
            Points = points0,
            Stroke = new SolidColorBrush(color0),
            StrokeThickness = 2,
        };
        var line1 = new Polyline
        {
            Points = points1,
            Stroke = new SolidColorBrush(color1),
            StrokeThickness = 2,
        };
        CurveCanvas.Children.Add(line0);
        CurveCanvas.Children.Add(line1);

        var brush = new SolidColorBrush(Colors.Black);
        const int yTicks = 5;
        for (int i = 0; i <= yTicks; i++)
        {
            int val = maxHp * i / yTicks;
            double y = topMargin + plotH - (double)val / maxHp * plotH;
            CurveCanvas.Children.Add(new Line
            {
                X1 = leftMargin - 4,
                Y1 = y,
                X2 = leftMargin,
                Y2 = y,
                Stroke = brush,
                StrokeThickness = 1,
            });
            var tb = new System.Windows.Controls.TextBlock
            {
                Text = val.ToString(),
                FontSize = 10,
                Foreground = brush,
            };
            System.Windows.Controls.Canvas.SetLeft(tb, leftMargin - 42);
            System.Windows.Controls.Canvas.SetTop(tb, y - 6);
            CurveCanvas.Children.Add(tb);
        }
        // 横轴：每秒 1 个小 tick；数字标注间隔随战斗时长变化：≥20s 每 5 秒，10–20s 每 2 秒，<10s 每 1 秒
        int maxTimeSec = maxTime / 1000;
        int labelIntervalMs = maxTimeSec >= 20 ? 5000 : maxTimeSec >= 10 ? 2000 : 1000;
        for (int timeMs = 0; timeMs <= maxTime; timeMs += 1000)
        {
            double x = leftMargin + (double)timeMs / maxTime * plotW;
            bool isLabelTick = timeMs % labelIntervalMs == 0;
            int tickHeight = isLabelTick ? 4 : 2;
            CurveCanvas.Children.Add(new Line
            {
                X1 = x,
                Y1 = topMargin + plotH,
                X2 = x,
                Y2 = topMargin + plotH + tickHeight,
                Stroke = brush,
                StrokeThickness = 1,
            });
            if (isLabelTick)
            {
                var tb = new System.Windows.Controls.TextBlock
                {
                    Text = (timeMs / 1000).ToString(),
                    FontSize = 10,
                    Foreground = brush,
                };
                System.Windows.Controls.Canvas.SetLeft(tb, x - 8);
                System.Windows.Controls.Canvas.SetTop(tb, topMargin + plotH + 6);
                CurveCanvas.Children.Add(tb);
            }
        }
        var xLabel = new System.Windows.Controls.TextBlock
        {
            Text = "时间/s",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = brush,
        };
        System.Windows.Controls.Canvas.SetLeft(xLabel, leftMargin + plotW / 2 - 24);
        System.Windows.Controls.Canvas.SetTop(xLabel, topMargin + plotH + 8);
        CurveCanvas.Children.Add(xLabel);
    }

}
