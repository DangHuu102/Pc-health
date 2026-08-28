using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCHealthDashboard.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PCHealthDashboard.ViewModels;

public partial class StorageCleanerViewModel : ObservableObject
{
    private readonly StorageCleanerService _cleanerService;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private ObservableCollection<string> _availableDrives = new();
    [ObservableProperty] private string _selectedDrive = "C:\\";
    [ObservableProperty] private ObservableCollection<CleanableItem> _items = new();
    
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isCleaning;
    [ObservableProperty] private string _statusMessage = "Sẵn sàng";

    private bool _selectAll = true;
    public bool SelectAll
    {
        get => _selectAll;
        set
        {
            SetProperty(ref _selectAll, value);
            foreach (var item in Items)
            {
                item.IsSelected = value;
            }
            UpdateTotalSize();
        }
    }
    
    [ObservableProperty] private long _totalSizeToClean;
    public string TotalSizeString => TotalSizeToClean > 1024 * 1024 * 1024 ? 
        $"{(double)TotalSizeToClean / (1024 * 1024 * 1024):F2} GB" : 
        $"{(double)TotalSizeToClean / (1024 * 1024):F1} MB";

    public StorageCleanerViewModel()
    {
        _cleanerService = new StorageCleanerService();
        LoadDrives();
    }

    private void LoadDrives()
    {
        AvailableDrives.Clear();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady && (drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable))
            {
                AvailableDrives.Add(drive.Name);
            }
        }
        if (AvailableDrives.Count > 0)
        {
            SelectedDrive = AvailableDrives[0];
        }
    }

    [RelayCommand]
    private async Task ScanJunk()
    {
        if (IsScanning || IsCleaning) return;
        
        IsScanning = true;
        Items.Clear();
        UpdateTotalSize();
        _cts = new CancellationTokenSource();

        try
        {
            var results = await Task.Run(() => 
                _cleanerService.ScanJunkFilesAsync(SelectedDrive, msg => 
                {
                    Application.Current.Dispatcher.Invoke(() => StatusMessage = msg);
                }, _cts.Token)
            );

            foreach (var item in results)
            {
                Items.Add(item);
            }
            
            StatusMessage = $"Quét rác hoàn tất. Tìm thấy {Items.Count} mục.";
            UpdateTotalSize();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task ScanDuplicates()
    {
        if (IsScanning || IsCleaning) return;
        
        IsScanning = true;
        Items.Clear();
        UpdateTotalSize();
        _cts = new CancellationTokenSource();

        try
        {
            var results = await Task.Run(() => 
                _cleanerService.ScanDuplicateFilesAsync(SelectedDrive, msg => 
                {
                    Application.Current.Dispatcher.Invoke(() => StatusMessage = msg);
                }, _cts.Token)
            );

            foreach (var item in results)
            {
                Items.Add(item);
            }
            
            StatusMessage = $"Quét file trùng lặp hoàn tất. Tìm thấy {Items.Count} file.";
            UpdateTotalSize();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task ScanDeepClean()
    {
        if (IsScanning || IsCleaning) return;
        
        IsScanning = true;
        Items.Clear();
        UpdateTotalSize();
        _cts = new CancellationTokenSource();

        try
        {
            var results = await Task.Run(() => 
                _cleanerService.ScanDeepJunkAsync(msg => 
                {
                    Application.Current.Dispatcher.Invoke(() => StatusMessage = msg);
                }, _cts.Token)
            );

            foreach (var item in results)
            {
                Items.Add(item);
            }
            
            StatusMessage = $"Quét chuyên sâu hoàn tất. Tìm thấy {Items.Count} mục.";
            UpdateTotalSize();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
        StatusMessage = "Đã hủy thao tác.";
    }

    [RelayCommand]
    private async Task RemoveSelected()
    {
        if (IsScanning || IsCleaning || Items.Count == 0) return;

        IsCleaning = true;
        
        try
        {
            var (successCount, bytesFreed) = await Task.Run(() => 
                _cleanerService.DeleteFilesAsync(Items, msg => 
                {
                    Application.Current.Dispatcher.Invoke(() => StatusMessage = msg);
                })
            );

            StatusMessage = $"Đã xóa {successCount} mục, giải phóng {(double)bytesFreed / (1024 * 1024):F1} MB.";
            
            // Remove deleted items from UI
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (Items[i].IsSelected)
                {
                    Items.RemoveAt(i);
                }
            }
            
            UpdateTotalSize();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi xóa file: {ex.Message}";
        }
        finally
        {
            IsCleaning = false;
        }
    }

    public void UpdateTotalSize()
    {
        long total = 0;
        foreach (var item in Items)
        {
            if (item.IsSelected) total += item.SizeBytes;
        }
        TotalSizeToClean = total;
        OnPropertyChanged(nameof(TotalSizeString));
    }
}
