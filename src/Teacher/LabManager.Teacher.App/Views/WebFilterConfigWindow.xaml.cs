using System.Windows;
using LabManager.Teacher.App.ViewModels;

namespace LabManager.Teacher.App.Views;

public partial class WebFilterConfigWindow : Window
{
    public WebFilterConfigWindow(WebFilterConfigViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.OnRequestClose += Close;
    }
}
