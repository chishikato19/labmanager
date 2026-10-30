using System.Windows;
using LabManager.Teacher.App.ViewModels;

namespace LabManager.Teacher.App.Views;

public partial class ScanMachinesWindow : Window
{
    public ScanMachinesWindow(ScanMachinesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
