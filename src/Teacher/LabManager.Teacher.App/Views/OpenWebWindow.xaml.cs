using System.Windows;
using LabManager.Teacher.App.ViewModels;

namespace LabManager.Teacher.App.Views;

public partial class OpenWebWindow : Window
{
    public OpenWebWindow(OpenWebViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.OnRequestClose += Close;
    }
}
