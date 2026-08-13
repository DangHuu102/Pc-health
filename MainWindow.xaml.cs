using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using PCHealthDashboard.Helpers;
using PCHealthDashboard.ViewModels;

namespace PCHealthDashboard;

public partial class MainWindow : Window
{
    private HotkeyHelper? _hotkeyHelper;
    private KittyWindow? _kittyWindow;

    public MainWindow()
    {
        InitializeComponent();
        
        this.SourceInitialized += MainWindow_SourceInitialized;
        this.Closed += MainWindow_Closed;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        _kittyWindow = new KittyWindow();
        
        _hotkeyHelper = new HotkeyHelper(helper.Handle, () =>
        {
            if (this.DataContext is MainViewModel vm)
            {
                _kittyWindow.TogglePopup(vm);
            }
        });

        // Hook into the ViewModel's poll timer to keep popup in sync
        if (this.DataContext is MainViewModel viewModel)
        {
            viewModel.DataPolled += () =>
            {
                if (_kittyWindow?.IsVisible == true)
                {
                    _kittyWindow.SyncAllValues(viewModel);
                }
            };
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _hotkeyHelper?.Dispose();
        _kittyWindow?.Close();
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            this.DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.WindowState == WindowState.Maximized)
        {
            this.WindowState = WindowState.Normal;
        }
        else
        {
            this.WindowState = WindowState.Maximized;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}