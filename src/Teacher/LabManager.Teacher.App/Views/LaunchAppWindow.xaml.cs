using System.Windows;
using LabManager.Teacher.App.ViewModels;

namespace LabManager.Teacher.App.Views;

public partial class LaunchAppWindow : Window
{
    public LaunchAppWindow(LaunchAppViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.OnRequestClose += Close;
    }
}
