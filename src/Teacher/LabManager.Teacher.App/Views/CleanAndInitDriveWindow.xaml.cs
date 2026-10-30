using System.Windows;
using LabManager.Teacher.App.ViewModels;

namespace LabManager.Teacher.App.Views;

public partial class CleanAndInitDriveWindow : Window
{
    public CleanAndInitDriveWindow(CleanAndInitDriveViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.OnRequestClose += Close;
    }
}
