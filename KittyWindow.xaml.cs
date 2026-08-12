using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using PCHealthDashboard.ViewModels;

namespace PCHealthDashboard;

public partial class KittyWindow : Window
{
    private bool _isPinned = true;
    private readonly ObservableValue _healthValue = new(0);
    private readonly ObservableValue _remainingValue = new(100);
    private readonly ObservableCollection<ObservableValue> _popupPingHistory = new();

    public KittyWindow()
    {
        InitializeComponent();

        // Setup health gauge (own series — cannot share with another chart)
        PopupHealthChart.Series = new ISeries[]
        {
            new PieSeries<ObservableValue>
            {
                Values = new[] { _healthValue },
                InnerRadius = 35,
                MaxRadialColumnWidth = 8,
                HoverPushout = 0,
                Fill = new SolidColorPaint(SKColor.Parse("#4ade80")),
                AnimationsSpeed = TimeSpan.Zero
            },
            new PieSeries<ObservableValue>
            {
                Values = new[] { _remainingValue },
                InnerRadius = 35,
                MaxRadialColumnWidth = 8,
                HoverPushout = 0,
                Fill = new SolidColorPaint(SKColor.Parse("#1e293b")),
                AnimationsSpeed = TimeSpan.Zero
            }
        };

        // Setup mini ping sparkline
        PopupPingChart.Series = new ISeries[]
        {
            new LineSeries<ObservableValue>
            {
                Values = _popupPingHistory,
                GeometrySize = 0,
                Stroke = new SolidColorPaint(SKColor.Parse("#06b6d4")) { StrokeThickness = 1.5f },
                Fill = null,
                LineSmoothness = 0.6,
                AnimationsSpeed = TimeSpan.Zero
            }
        };
        PopupPingChart.XAxes = new[] { new Axis { IsVisible = false } };
        PopupPingChart.YAxes = new[] { new Axis { IsVisible = false } };

        // Position at bottom right
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 20;
    }

    public void TogglePopup(MainViewModel vm)
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            SyncAllValues(vm);
            Show();
        }
    }

    /// <summary>
    /// Copies every value from the ViewModel into the popup controls.
    /// Called from MainWindow on every poll tick to stay perfectly in sync.
    /// </summary>
    public void SyncAllValues(MainViewModel vm)
    {
        if (!IsVisible) return;

        // ── Health Score ──
        int score = vm.HealthScore;
        if (_healthValue.Value != score) _healthValue.Value = score;
        if (_remainingValue.Value != Math.Max(0, 100 - score)) _remainingValue.Value = Math.Max(0, 100 - score);
        GaugeScoreText.Text = score.ToString();
        ScoreValueText.Text = score.ToString();
        StatusLabel.Text = score >= 80 ? "Excellent" : score >= 60 ? "Good" : score >= 40 ? "Warning" : "Critical";

        // ── CPU ──
        PopupCpuTemp.Text = $"{vm.CpuTemp:F0}°C";
        PopupCpuBar.Value = vm.CpuUsage;
        PopupCpuPct.Text = $"{vm.CpuUsage:F0}%";
        CpuDot.Fill = GetStatusBrush(vm.CpuUsage);

        // ── GPU ──
        float gpuTemp = vm.HasGpu1 ? vm.Gpu1Temp : vm.Gpu0Temp;
        float gpuUsage = vm.HasGpu1 ? vm.Gpu1Usage : vm.Gpu0Usage;
        
        PopupGpuTemp.Text = $"{gpuTemp:F0}°C";
        PopupGpuBar.Value = gpuUsage;
        PopupGpuPct.Text = $"{gpuUsage:F0}%";
        GpuDot.Fill = GetStatusBrush(gpuUsage);

        // ── RAM ──
        float ramPct = vm.RamTotal > 0 ? (vm.RamUsed / vm.RamTotal) * 100f : 0;
        PopupRamDetail.Text = $"{vm.RamUsed:F1} / {vm.RamTotal:F0} GB";
        PopupRamBar.Value = ramPct;
        PopupRamPct.Text = $"{ramPct:F0}%";
        RamDot.Fill = GetStatusBrush(ramPct);

        // ── Storage ──
        float storagePct = vm.SsdTotalSpace > 0 ? (vm.SsdUsedSpace / vm.SsdTotalSpace) * 100f : 0;
        PopupStorageDetail.Text = $"SSD Health: {vm.SsdHealth:F0}%";
        PopupStorageBar.Value = storagePct;
        PopupStoragePct.Text = $"{storagePct:F0}%";
        StorageDot.Fill = GetStatusBrush(storagePct);

        // ── Network ──
        PopupDownloadText.Text = $"↓ {vm.DownloadMbps:F1} Mbps";
        PopupUploadText.Text = $"↑ {vm.UploadMbps:F1} Mbps";
        
        bool isStable = vm.PingLatency < 100;
        bool isModerate = vm.PingLatency < 200;
        
        NetDot.Fill = isStable 
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4ade80")!) 
            : isModerate 
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f59e0b")!) 
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444")!);

        // Sync sparkline from ViewModel's NetworkSpeedHistory
        SyncSparkline(vm.NetworkSpeedHistory);

        // ── Footer ──
        FooterText.Text = score >= 80 ? "All systems are running smoothly." 
                        : score >= 60 ? "System is running with minor issues." 
                        : "⚠ Attention needed on some components.";
    }

    private void SyncSparkline(ObservableCollection<ObservableValue> source)
    {
        // Keep popup sparkline in sync with the main ViewModel's ping history
        while (_popupPingHistory.Count > source.Count)
            _popupPingHistory.RemoveAt(_popupPingHistory.Count - 1);

        for (int i = 0; i < source.Count; i++)
        {
            double val = source[i].Value ?? 0;
            if (i < _popupPingHistory.Count)
                _popupPingHistory[i].Value = val;
            else
                _popupPingHistory.Add(new ObservableValue(val));
        }
    }

    private static SolidColorBrush GetStatusBrush(float pct)
    {
        if (pct < 70) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4ade80")!);
        if (pct < 90) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f59e0b")!);
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444")!);
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = !_isPinned;
        Topmost = _isPinned;
        PinButton.Content = _isPinned ? "📌" : "📍";
    }

    private void ClosePopup_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}
