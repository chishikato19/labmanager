using System.Windows;
using LabManager.Teacher.App.ViewModels;

namespace LabManager.Teacher.App.Views;

public partial class BlacklistConfigWindow : Window
{
    public BlacklistConfigWindow(BlacklistConfigViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.OnRequestClose += Close;
    }
}
