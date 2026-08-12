using System.Windows;
using System.Windows.Input;

namespace PCHealthDashboard;

public partial class StorageCleanerWindow : Window
{
    public StorageCleanerWindow()
    {
        InitializeComponent();
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}
