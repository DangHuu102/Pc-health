using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCHealthDashboard.Helpers;
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
    [ObservableProperty] private float _gpu0Temp;
    [ObservableProperty] private float _gpu0Usage;
    [ObservableProperty] private float _gpu0Vram;
    [ObservableProperty] private string _gpu0Name = "GPU 0";

    [ObservableProperty] private bool _hasGpu1;
    [ObservableProperty] private float _gpu1Temp;
    [ObservableProperty] private float _gpu1Usage;
    [ObservableProperty] private float _gpu1Vram;
    [ObservableProperty] private string _gpu1Name = "GPU 1";
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

    [ObservableProperty] private string _systemUptime = "00:00:00";


    [ObservableProperty] private bool _isAppActive = true;

    [ObservableProperty] private ObservableCollection<WarningAlert> _systemAlerts = new();
    
    public ObservableCollection<DriveStatus> Drives { get; } = new();

    public ObservableCollection<ObservableValue> CpuHistory { get; } = new();
    public ObservableCollection<ObservableValue> Gpu0History { get; } = new();
    public ObservableCollection<ObservableValue> Gpu1History { get; } = new();
    public ObservableCollection<ObservableValue> RamHistory { get; } = new();
    public ObservableCollection<ObservableValue> StorageHistory { get; } = new();
    public ObservableCollection<ObservableValue> PingHistory { get; } = new();
    public ObservableCollection<ObservableValue> LossHistory { get; } = new();
    public ObservableCollection<ObservableValue> NetworkSpeedHistory { get; } = new();
    
    public ISeries[] CpuSeries { get; set; }
    public ISeries[] Gpu0Series { get; set; }
    public ISeries[] Gpu1Series { get; set; }
    public ISeries[] GpuCombinedSeries { get; set; }
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
        Gpu0Series = CreateSeries(Gpu0History, colorGpu);
        
        var colorGpu1 = SKColor.Parse("#ef4444"); // Red for GPU1
        Gpu1Series = CreateSeries(Gpu1History, colorGpu1);
        GpuCombinedSeries = _hardwareService.HasGpu1 ? new[] { Gpu0Series[0], Gpu1Series[0] } : new[] { Gpu0Series[0] };
        
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
                Fill = new SolidColorPaint(colorRam),
                AnimationsSpeed = TimeSpan.Zero
            },
            new PieSeries<ObservableValue> 
            { 
                Values = new[] { remainingValue }, 
                InnerRadius = 35, 
                MaxRadialColumnWidth = 8,
                HoverPushout = 0,
                Fill = new SolidColorPaint(SKColor.Parse("#1e293b")),
                AnimationsSpeed = TimeSpan.Zero
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
            Interval = TimeSpan.FromSeconds(2)
        };
        _pollTimer.Tick += async (s, e) => await PollDataAsync();
        _pollTimer.Start();

        // Initial working set memory trim
        MemoryHelper.MinimizeMemory();
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
                GeometrySize = 0,
                GeometryFill = null,
                GeometryStroke = null,
                Stroke = new SolidColorPaint(color) { StrokeThickness = 2 },
                LineSmoothness = 0,
                AnimationsSpeed = TimeSpan.Zero
            } 
        };
    }

    public void OnAppActivated()
    {
    }

    public void OnAppDeactivated()
    {
        MemoryHelper.MinimizeMemory();
    }

    private bool _isPolling;
    private int _pollCounter;
    public event Action? DataPolled;

    [RelayCommand]
    private void FreeRam()
    {
        bool success = MemoryHelper.ClearSystemMemoryCache();
        
        var newAlert = new WarningAlert(
            "Tối ưu RAM", 
            success ? "Đã ép tất cả ứng dụng/tab trình duyệt nhả RAM thừa và giải phóng Standby List thành công." : "Đã dọn dẹp RAM ứng dụng. Cần chạy PC Health bằng quyền Admin để giải phóng toàn bộ RAM của hệ thống.", 
            success ? "#4ade80" : "#f59e0b", 
            "#222");

        if (SystemAlerts.Count > 0 && SystemAlerts[0].Title == "Tối ưu RAM")
        {
            SystemAlerts.RemoveAt(0);
        }
        SystemAlerts.Insert(0, newAlert);
    }

    [RelayCommand]
    private void OpenStorageCleaner()
    {
        var window = new StorageCleanerWindow();
        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
    }

    private async Task PollDataAsync()
    {
        if (_isPolling) return;

        try
        {
            _isPolling = true;
            float cpuTemp = 45f, cpuUsage = 0f;
            float ramUsed = 0f, ramTotal = 16f;
            List<DriveStatus>? drives = null;
            
            await Task.Run(() =>
            {
                _hardwareService.Update();

                var cpuStats = _hardwareService.GetCpuStats();
                cpuTemp = cpuStats.Temp > 0 ? cpuStats.Temp : 45f;
                cpuUsage = cpuStats.Usage;

                var gpu0Stats = _hardwareService.GetGpu0Stats();
                Gpu0Temp = gpu0Stats.Temp > 0 ? gpu0Stats.Temp : 40f;
                Gpu0Usage = gpu0Stats.Load;
                Gpu0Vram = gpu0Stats.VramUsed;

                HasGpu1 = _hardwareService.HasGpu1;
                if (HasGpu1)
                {
                    var gpu1Stats = _hardwareService.GetGpu1Stats();
                    Gpu1Temp = gpu1Stats.Temp > 0 ? gpu1Stats.Temp : 40f;
                    Gpu1Usage = gpu1Stats.Load;
                    Gpu1Vram = gpu1Stats.VramUsed;
                }

                var ramStats = _hardwareService.GetRamStats();
                ramUsed = ramStats.UsedGB;
                ramTotal = ramStats.TotalGB > 0 ? ramStats.TotalGB : 16f;

                drives = _hardwareService.GetDrivesStats();
            });

            CpuTemp = cpuTemp;
            CpuUsage = cpuUsage;
            Gpu0Name = _hardwareService.Gpu0Name;
            Gpu1Name = _hardwareService.Gpu1Name;
            RamUsed = ramUsed;
            RamTotal = ramTotal;

            if (drives == null) drives = new List<DriveStatus>();
        
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
                SsdHealth = drives.Min(d => d.Health);
                SsdTemp = drives.Max(d => d.Temp);
                SsdFreeSpace = drives.Sum(d => d.FreeGB);
                SsdTotalSpace = drives.Sum(d => d.TotalGB);
                SsdUsedSpace = drives.Sum(d => d.UsedGB);
            }

            var netStats = await _networkService.GetNetworkStatusAsync();
            PingLatency = netStats.Latency;
            PacketLoss = netStats.PacketLoss;
            DownloadMbps = netStats.DownloadMbps;
            UploadMbps = netStats.UploadMbps;

            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            SystemUptime = uptime.Days > 0 
                ? $"{uptime.Days}d {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}"
                : $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";

            HealthScore = _healthAnalyzer.CalculateHealthScore(
                SsdHealth, SsdFreeSpace, SsdTotalSpace,
                CpuTemp, Gpu0Temp,
                RamUsed, RamTotal,
                PacketLoss);

            UpdateAlerts();

            Application.Current?.Dispatcher.Invoke(() =>
            {
                CpuHistory.Add(new ObservableValue(CpuUsage));
                Gpu0History.Add(new ObservableValue(Gpu0Usage));
                if (HasGpu1) Gpu1History.Add(new ObservableValue(Gpu1Usage));
                
                RamHistory.Add(new ObservableValue(RamTotal > 0 ? (RamUsed / RamTotal) * 100 : 0));
                StorageHistory.Add(new ObservableValue(SsdTotalSpace > 0 ? ((SsdTotalSpace - SsdFreeSpace) / SsdTotalSpace) * 100 : 0));
                PingHistory.Add(new ObservableValue(PingLatency));
                LossHistory.Add(new ObservableValue(PacketLoss));
                NetworkSpeedHistory.Add(new ObservableValue(DownloadMbps));
                
                if (HealthSeries[0].Values is ObservableValue[] hVals && hVals.Length > 0)
                {
                    if (hVals[0].Value != HealthScore) hVals[0].Value = HealthScore;
                }
                if (HealthSeries[1].Values is ObservableValue[] rVals && rVals.Length > 0)
                {
                    if (rVals[0].Value != 100 - HealthScore) rVals[0].Value = 100 - HealthScore;
                }

                const int maxHistory = 60;
                if (CpuHistory.Count > maxHistory) CpuHistory.RemoveAt(0);
                if (Gpu0History.Count > maxHistory) Gpu0History.RemoveAt(0);
                if (HasGpu1 && Gpu1History.Count > maxHistory) Gpu1History.RemoveAt(0);
                if (RamHistory.Count > maxHistory) RamHistory.RemoveAt(0);
                if (StorageHistory.Count > maxHistory) StorageHistory.RemoveAt(0);
                if (PingHistory.Count > maxHistory) PingHistory.RemoveAt(0);
                if (LossHistory.Count > maxHistory) LossHistory.RemoveAt(0);
                if (NetworkSpeedHistory.Count > maxHistory) NetworkSpeedHistory.RemoveAt(0);
                
                DataPolled?.Invoke();
            });

            _pollCounter++;
            if (_pollCounter % 30 == 0)
            {
                MemoryHelper.MinimizeMemory();
            }
            else if (_pollCounter % 3 == 0)
            {
                MemoryHelper.TrimWorkingSet();
            }
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
        if (Gpu0Temp > 85) SystemAlerts.Add(new WarningAlert($"Nhiệt độ {Gpu0Name} cao!", $"Nhiệt độ hiện tại: {Gpu0Temp:F0}°C", "#ef4444", "#2A1518"));
        if (HasGpu1 && Gpu1Temp > 85) SystemAlerts.Add(new WarningAlert($"Nhiệt độ {Gpu1Name} cao!", $"Nhiệt độ hiện tại: {Gpu1Temp:F0}°C", "#ef4444", "#2A1518"));
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
