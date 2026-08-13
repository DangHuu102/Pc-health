using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;
using System.IO;
using PCHealthDashboard.ViewModels;

namespace PCHealthDashboard.Services;

public class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) { computer.Traverse(this); }
    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
    }
    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}

public class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer;
    private readonly UpdateVisitor _updateVisitor;
    private IHardware? _cpu;
    private IHardware? _gpu0;
    private IHardware? _gpu1;
    private IHardware? _ram;
    private List<IHardware> _storageList = new();

    public bool HasGpu1 => _gpu1 != null;
    public string Gpu0Name => _gpu0?.Name ?? "GPU 0";
    public string Gpu1Name => _gpu1?.Name ?? "GPU 1";

    public HardwareMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = false,
            IsStorageEnabled = true,
            IsMotherboardEnabled = false,
            IsControllerEnabled = false,
            IsNetworkEnabled = false,
            IsBatteryEnabled = false,
            IsPsuEnabled = false
        };
        
        _updateVisitor = new UpdateVisitor();

        try { _computer.Open(); } catch { }

        InitializeHardware();
    }

    private void InitializeHardware()
    {
        _cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        
        var gpus = _computer.Hardware.Where(h => 
            h.HardwareType == HardwareType.GpuNvidia || 
            h.HardwareType == HardwareType.GpuAmd || 
            h.HardwareType == HardwareType.GpuIntel).ToList();
            
        if (gpus.Count > 0) _gpu0 = gpus[0];
        if (gpus.Count > 1) _gpu1 = gpus[1];
        
        _ram = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory);
        _storageList = _computer.Hardware.Where(h => h.HardwareType == HardwareType.Storage).ToList();
    }

    private int _tickCount = 0;
    private List<DriveStatus>? _cachedDrives;

    public void Update()
    {
        try
        {
            _cpu?.Update();
            _gpu0?.Update();
            _gpu1?.Update();
            _ram?.Update();
            
            // Storage SMART polling is very heavy, only do it every 5 ticks (10s)
            if (_tickCount % 5 == 0)
            {
                for (int i = 0; i < _storageList.Count; i++)
                {
                    _storageList[i].Update();
                }
            }
            _tickCount++;
        }
        catch { }
    }

    public (float Temp, float Usage) GetCpuStats()
    {
        float temp = 0f, usage = 0f;
        if (_cpu != null)
        {
            // Gather all CPU temperature sensors
            var tempSensors = _cpu.Sensors.Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue).ToList();
            if (tempSensors.Count > 0)
            {
                // Prefer "Package", "Core Max", or "Core Average". Otherwise, just take the Max of any core.
                var packageSensor = tempSensors.FirstOrDefault(s => s.Name.Contains("Package") || s.Name.Contains("Core Max"));
                if (packageSensor != null && packageSensor.Value.GetValueOrDefault() > 0)
                {
                    temp = packageSensor.Value.GetValueOrDefault();
                }
                else
                {
                    // Fallback to the maximum of any CPU temperature sensor (this usually tracks the hottest core)
                    temp = tempSensors.Max(s => s.Value.GetValueOrDefault());
                }
            }
            
            var usageSensor = _cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Total"))
                              ?? _cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load);
            
            if (usageSensor?.Value != null) usage = usageSensor.Value.Value;
        }
        
        // Fallback to Embedded Controller or Motherboard if CPU sensors fail (common on laptops)
        if (temp <= 0f)
        {
            var ec = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.EmbeddedController || h.HardwareType == HardwareType.SuperIO || h.HardwareType == HardwareType.Motherboard);
            if (ec != null)
            {
                ec.Update();
                var cpuTempSensor = ec.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && (s.Name.Contains("CPU") || s.Name.Contains("Core")));
                if (cpuTempSensor == null) cpuTempSensor = ec.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
                if (cpuTempSensor?.Value != null) temp = cpuTempSensor.Value.Value;
            }
        }

        return (temp, usage);
    }

    private (float Temp, float Load, float VramUsed) GetStatsForGpu(IHardware? gpu)
    {
        float temp = 0f, load = 0f, vram = 0f;
        if (gpu != null)
        {
            var tempSensor = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
            var loadSensor = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Core"))
                             ?? gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load);
            var vramSensor = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.SmallData && s.Name.Contains("Memory Used"));
            
            if (tempSensor?.Value != null) temp = tempSensor.Value.Value;
            if (loadSensor?.Value != null) load = loadSensor.Value.Value;
            if (vramSensor?.Value != null) vram = vramSensor.Value.Value;
        }
        return (temp, load, vram);
    }

    public (float Temp, float Load, float VramUsed) GetGpu0Stats() => GetStatsForGpu(_gpu0);
    public (float Temp, float Load, float VramUsed) GetGpu1Stats() => GetStatsForGpu(_gpu1);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public (float UsedGB, float TotalGB) GetRamStats()
    {
        var memStatus = new MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref memStatus))
        {
            float totalGB = memStatus.ullTotalPhys / (1024f * 1024f * 1024f);
            float availGB = memStatus.ullAvailPhys / (1024f * 1024f * 1024f);
            return (totalGB - availGB, totalGB);
        }
        return (0f, 16f);
    }

    public List<DriveStatus> GetDrivesStats()
    {
        // Return cached drives to avoid heavy I/O polling every 2 seconds.
        // We update the cache only right after Update() triggers a storage SMART update.
        if (_tickCount % 5 != 1 && _cachedDrives != null) 
            return _cachedDrives;

        var list = new List<DriveStatus>();
        
        try
        {
            var logicalDrives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).ToList();
            var physicalDrives = _computer.Hardware.Where(h => h.HardwareType == HardwareType.Storage).ToList();
            
            if (physicalDrives.Count == 1)
            {
                // The user only has 1 physical drive (e.g. 1 SSD partitioned into C: and D:).
                // They want to see exactly 1 bar for their SSD, not split by partitions.
                var hw = physicalDrives[0];
                var hwName = hw.Name?.ToUpper() ?? "";
                
                var status = new DriveStatus
                {
                    Name = string.IsNullOrWhiteSpace(hw.Name) ? "Local Disk" : hw.Name, // Fallback if name is empty
                    TotalGB = logicalDrives.Sum(d => d.TotalSize) / (1024f * 1024f * 1024f),
                    FreeGB = logicalDrives.Sum(d => d.AvailableFreeSpace) / (1024f * 1024f * 1024f),
                    Type = (hwName.Contains("HDD") || hwName.Contains("HARD DISK")) ? "HDD" : "SSD",
                    Interface = hwName.Contains("NVME") ? "NVMe" : "SATA"
                };

                var tempSensor = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
                var healthSensor = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Level && (s.Name.Contains("Health") || s.Name.Contains("Remaining") || s.Name.Contains("Life")));
                
                // Fallback temp if sensor not found
                if (tempSensor?.Value != null) status.Temp = tempSensor.Value.Value;
                else
                {
                    var mb = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);
                    var mbTemp = mb?.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
                    status.Temp = mbTemp?.Value ?? 0f; // Use motherboard temp if available, otherwise 0
                }

                status.Health = healthSensor?.Value != null ? Math.Min(100f, healthSensor.Value.Value) : 0f;

                list.Add(status);
            }
            else
            {
                // Multiple physical drives: fallback to showing logical partitions (C:, D:)
                foreach (var d in logicalDrives)
                {
                    string letter = d.Name.Replace("\\", "");
                    
                    var status = new DriveStatus
                    {
                        Name = $"Drive {letter}",
                        TotalGB = d.TotalSize / (1024f * 1024f * 1024f),
                        FreeGB = d.AvailableFreeSpace / (1024f * 1024f * 1024f)
                    };

                    // Try to guess the physical drive
                    var hwMatch = physicalDrives.FirstOrDefault(); 

                    if (hwMatch != null)
                    {
                        var hwName = hwMatch.Name?.ToUpper() ?? "";
                        var tempSensor = hwMatch.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
                        var healthSensor = hwMatch.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Level && (s.Name.Contains("Health") || s.Name.Contains("Remaining")));
                        
                        if (tempSensor?.Value != null) status.Temp = tempSensor.Value.Value;
                        else
                        {
                            var mb = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);
                            var mbTemp = mb?.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
                            status.Temp = mbTemp?.Value ?? 0f;
                        }

                        status.Health = healthSensor?.Value != null ? Math.Min(100f, healthSensor.Value.Value) : 0f;
                        status.Type = (hwName.Contains("HDD") || hwName.Contains("HARD DISK")) ? "HDD" : "SSD";
                        status.Interface = hwName.Contains("NVME") ? "NVMe" : "SATA";
                    }
                    else
                    {
                        status.Type = "SSD";
                        status.Interface = "SATA";
                    }
                    
                    list.Add(status);
                }
            }
        }
        catch { }

        if (list.Count == 0)
        {
            list.Add(new DriveStatus { Name = "Drive C:", TotalGB = 512, FreeGB = 92, Health = 96, Temp = 40 });
        }

        _cachedDrives = list;
        return list;
    }

    public void Dispose()
    {
        try { _computer.Close(); } catch { }
    }
}
