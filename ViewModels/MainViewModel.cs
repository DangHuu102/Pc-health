using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PCHealthDashboard.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace PCHealthDashboard.ViewModels;

public record WarningAlert(string Title, string Message, string Color, string BgColor);

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly HardwareMonitorService _hardwareService;
    private readonly NetworkMonitorService _networkService;
    private readonly HealthAnalyzerService _healthAnalyzer;
    private readonly DispatcherTimer _pollTimer;

    [ObservableProperty] private string _currentView = "Tổng quan";

    [ObservableProperty] private int _healthScore;
    [ObservableProperty] private float _cpuTemp;
    [ObservableProperty] private float _cpuUsage;
    [ObservableProperty] private float _gpuTemp;
    [ObservableProperty] private float _gpuUsage;
    [ObservableProperty] private float _gpuVram;
    [ObservableProperty] private float _ramUsed;
    [ObservableProperty] private float _ramTotal;
    [ObservableProperty] private float _ssdHealth;
    [ObservableProperty] private float _ssdTemp;
    [ObservableProperty] private float _ssdFreeSpace;
    [ObservableProperty] private float _ssdTotalSpace;
    [ObservableProperty] private float _ssdUsedSpace;
    [ObservableProperty] private long _pingLatency;
    [ObservableProperty] private double _packetLoss;
    [ObservableProperty] private double _downloadMbps;
    [ObservableProperty] private double _uploadMbps;
    [ObservableProperty] private bool _isAppActive = true;

    [ObservableProperty] private ObservableCollection<WarningAlert> _systemAlerts = new();
    
    public ObservableCollection<DriveStatus> Drives { get; } = new();

    public ObservableCollection<ObservableValue> CpuHistory { get; } = new();
    public ObservableCollection<ObservableValue> GpuHistory { get; } = new();
    public ObservableCollection<ObservableValue> RamHistory { get; } = new();
    public ObservableCollection<ObservableValue> StorageHistory { get; } = new();
    public ObservableCollection<ObservableValue> PingHistory { get; } = new();
    public ObservableCollection<ObservableValue> LossHistory { get; } = new();
    public ObservableCollection<ObservableValue> NetworkSpeedHistory { get; } = new();
    
    public ISeries[] CpuSeries { get; set; }
    public ISeries[] GpuSeries { get; set; }
    public ISeries[] RamSeries { get; set; }
    public ISeries[] StorageSeries { get; set; }
    public ISeries[] PingSeries { get; set; }
    public ISeries[] LossSeries { get; set; }
    public ISeries[] HealthSeries { get; set; }

    public Axis[] ChartXAxes { get; set; }
    public Axis[] ChartYAxes { get; set; }

    public MainViewModel()
    {
        _hardwareService = new HardwareMonitorService();
        _networkService = new NetworkMonitorService();
        _healthAnalyzer = new HealthAnalyzerService();

        // Colors based on reference image
        var colorCpu = SKColor.Parse("#3b82f6"); // Blue
        var colorGpu = SKColor.Parse("#a855f7"); // Purple
        var colorRam = SKColor.Parse("#4ade80"); // Green
        var colorStorage = SKColor.Parse("#f59e0b"); // Orange/Yellow
        var colorPing = SKColor.Parse("#06b6d4"); // Cyan
        var colorLoss = SKColor.Parse("#0ea5e9"); // Light Blue

        CpuSeries = CreateSeries(CpuHistory, colorCpu);
        GpuSeries = CreateSeries(GpuHistory, colorGpu);
        RamSeries = CreateSeries(RamHistory, colorRam);
        StorageSeries = CreateSeries(StorageHistory, colorStorage);
        PingSeries = CreateSeries(PingHistory, colorPing);
        LossSeries = CreateSeries(LossHistory, colorLoss);
        
        var healthValue = new ObservableValue(100);
        var remainingValue = new ObservableValue(0);
        HealthSeries = new ISeries[]
        {
            new PieSeries<ObservableValue> 
            { 
                Values = new[] { healthValue }, 
                InnerRadius = 35, 
                MaxRadialColumnWidth = 8,
                HoverPushout = 0,
                Fill = new SolidColorPaint(colorRam) 
            },
            new PieSeries<ObservableValue> 
            { 
                Values = new[] { remainingValue }, 
                InnerRadius = 35, 
                MaxRadialColumnWidth = 8,
                HoverPushout = 0,
                Fill = new SolidColorPaint(SKColor.Parse("#1e293b")) 
            }
        };

        ChartXAxes = new Axis[] 
        { 
            new Axis 
            { 
                ShowSeparatorLines = false,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#666666")),
                TextSize = 10,
                Padding = new LiveChartsCore.Drawing.Padding(0, 10, 0, 0)
            } 
        };
        ChartYAxes = new Axis[] 
        { 
            new Axis 
            { 
                ShowSeparatorLines = true,
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#2A2F3D")) { StrokeThickness = 1 },
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#666666")),
                TextSize = 10,
                MinLimit = 0
            } 
        };

        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _pollTimer.Tick += async (s, e) => await PollDataAsync();
        _pollTimer.Start();
    }

    private ISeries[] CreateSeries(ObservableCollection<ObservableValue> history, SKColor color)
    {
        var fillColor = color.WithAlpha(50); // Semi-transparent for area fill
        return new ISeries[] 
        { 
            new LineSeries<ObservableValue> 
            { 
                Values = history, 
                Fill = new SolidColorPaint(fillColor),
                GeometryFill = null,
                GeometryStroke = null,
                Stroke = new SolidColorPaint(color) { StrokeThickness = 2 },
                LineSmoothness = 0.5
            } 
        };
    }

    public void OnAppActivated()
    {
    }

    public void OnAppDeactivated()
    {
    }

    private bool _isPolling;
    public event Action? DataPolled;

    private async Task PollDataAsync()
    {
        if (_isPolling) return;

        try
        {
            _isPolling = true;
            _hardwareService.Update();

            var cpuStats = _hardwareService.GetCpuStats();
            CpuTemp = cpuStats.Temp > 0 ? cpuStats.Temp : 45f; // Safe default if sensor unavailable on some VMs
            CpuUsage = cpuStats.Usage;

            var gpuStats = _hardwareService.GetGpuStats();
            GpuTemp = gpuStats.Temp > 0 ? gpuStats.Temp : 40f;
            GpuUsage = gpuStats.Load;
            GpuVram = gpuStats.VramUsed;

        var ramStats = _hardwareService.GetRamStats();
        RamUsed = ramStats.UsedGB;
        RamTotal = ramStats.TotalGB > 0 ? ramStats.TotalGB : 16f;

        var drives = _hardwareService.GetDrivesStats();
        
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (Drives.Count == drives.Count)
            {
                for (int i = 0; i < drives.Count; i++)
                {
                    Drives[i].Name = drives[i].Name;
                    Drives[i].Type = drives[i].Type;
                    Drives[i].Interface = drives[i].Interface;
                    Drives[i].TotalGB = drives[i].TotalGB;
                    Drives[i].FreeGB = drives[i].FreeGB;
                    Drives[i].Health = drives[i].Health;
                    Drives[i].Temp = drives[i].Temp;
                }
            }
            else
            {
                Drives.Clear();
                foreach (var d in drives) Drives.Add(d);
            }
        });

        if (drives.Count > 0)
        {
            var systemDrive = drives.FirstOrDefault(d => d.Name.Contains("C")) ?? drives[0];
            SsdHealth = systemDrive.Health;
            SsdTemp = systemDrive.Temp;
            SsdFreeSpace = systemDrive.FreeGB;
            SsdTotalSpace = systemDrive.TotalGB;
            SsdUsedSpace = systemDrive.UsedGB;
        }

        var netStats = await _networkService.GetNetworkStatusAsync();
        PingLatency = netStats.Latency;
        PacketLoss = netStats.PacketLoss;
        DownloadMbps = netStats.DownloadMbps;
        UploadMbps = netStats.UploadMbps;

        HealthScore = _healthAnalyzer.CalculateHealthScore(
            SsdHealth, SsdFreeSpace, SsdTotalSpace,
            CpuTemp, GpuTemp,
            RamUsed, RamTotal,
            PacketLoss);

        UpdateAlerts();

        Application.Current?.Dispatcher.Invoke(() =>
        {
            CpuHistory.Add(new ObservableValue(CpuUsage));
            GpuHistory.Add(new ObservableValue(GpuUsage));
            RamHistory.Add(new ObservableValue(RamTotal > 0 ? (RamUsed / RamTotal) * 100 : 0));
            StorageHistory.Add(new ObservableValue(SsdTotalSpace > 0 ? ((SsdTotalSpace - SsdFreeSpace) / SsdTotalSpace) * 100 : 0));
            PingHistory.Add(new ObservableValue(PingLatency));
            LossHistory.Add(new ObservableValue(PacketLoss));
            NetworkSpeedHistory.Add(new ObservableValue(DownloadMbps));
            
            if (HealthSeries[0].Values != null)
            {
                var hVals = (ObservableValue[])HealthSeries[0].Values;
                hVals[0].Value = HealthScore;
            }
            if (HealthSeries[1].Values != null)
            {
                var rVals = (ObservableValue[])HealthSeries[1].Values;
                rVals[0].Value = 100 - HealthScore;
            }

            if (CpuHistory.Count > 1800) CpuHistory.RemoveAt(0);
            if (GpuHistory.Count > 1800) GpuHistory.RemoveAt(0);
            if (RamHistory.Count > 1800) RamHistory.RemoveAt(0);
            if (StorageHistory.Count > 1800) StorageHistory.RemoveAt(0);
            if (PingHistory.Count > 1800) PingHistory.RemoveAt(0);
            if (LossHistory.Count > 1800) LossHistory.RemoveAt(0);
            if (NetworkSpeedHistory.Count > 1800) NetworkSpeedHistory.RemoveAt(0);
            
            DataPolled?.Invoke();
        });

        }
        finally
        {
            _isPolling = false;
        }
    }

    private void UpdateAlerts()
    {
        SystemAlerts.Clear();

        if (CpuTemp > 85) SystemAlerts.Add(new WarningAlert("Nhiệt độ CPU cao!", $"Nhiệt độ hiện tại: {CpuTemp:F0}°C", "#ef4444", "#2A1518"));
        if (GpuTemp > 85) SystemAlerts.Add(new WarningAlert("Nhiệt độ GPU cao!", $"Nhiệt độ hiện tại: {GpuTemp:F0}°C", "#ef4444", "#2A1518"));
        if (RamTotal > 0 && (RamUsed / RamTotal) > 0.9) SystemAlerts.Add(new WarningAlert("RAM bị đầy!", $"Sử dụng {RamUsed:F1} GB / {RamTotal:F1} GB", "#ef4444", "#2A1518"));
        
        if (SsdTotalSpace > 0)
        {
            float freePercent = (SsdFreeSpace / SsdTotalSpace) * 100f;
            if (freePercent < 10) SystemAlerts.Add(new WarningAlert("Ổ C sắp đầy!", $"Chỉ còn {SsdFreeSpace:F1} GB trống ({freePercent:F0}%)", "#fbbf24", "#2A2210"));
        }
        
        if (PingLatency > 200) SystemAlerts.Add(new WarningAlert("Mạng lag bất thường!", $"Ping: {PingLatency} ms | Loss: {PacketLoss}%", "#fbbf24", "#2A2210"));

        if (SystemAlerts.Count == 0) SystemAlerts.Add(new WarningAlert("Hệ thống ổn định", "Mọi thông số đều đang ở mức an toàn.", "#4ade80", "#16281b"));
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void SwitchView(string viewName)
    {
        CurrentView = viewName;
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _hardwareService.Dispose();
    }
}
