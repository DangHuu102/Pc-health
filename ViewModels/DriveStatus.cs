using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace PCHealthDashboard.ViewModels;

public partial class DriveStatus : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _type = "SSD";
    [ObservableProperty] private string _interface = "NVMe";
    [ObservableProperty] private float _totalGB;
    [ObservableProperty] private float _freeGB;
    [ObservableProperty] private float _health = 100f;
    [ObservableProperty] private float _temp = 40f;

    public float UsedGB => TotalGB - FreeGB;
    public float UsedPercent => TotalGB > 0 ? (UsedGB / TotalGB) * 100f : 0f;
    public string HealthString => Health > 0 ? $"{Math.Min(100, Health):F0}%" : "N/A";
    public string TempString => Temp > 0 ? $"{Temp:F0}°C" : "N/A";

    partial void OnTotalGBChanged(float value)
    {
        OnPropertyChanged(nameof(UsedGB));
        OnPropertyChanged(nameof(UsedPercent));
    }

    partial void OnFreeGBChanged(float value)
    {
        OnPropertyChanged(nameof(UsedGB));
        OnPropertyChanged(nameof(UsedPercent));
    }

    partial void OnHealthChanged(float value)
    {
        OnPropertyChanged(nameof(HealthString));
    }

    partial void OnTempChanged(float value)
    {
        OnPropertyChanged(nameof(TempString));
    }
}
