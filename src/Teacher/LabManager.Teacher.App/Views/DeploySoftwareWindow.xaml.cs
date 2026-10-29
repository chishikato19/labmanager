using System.Windows;
using System.Windows.Controls;
using LabManager.Teacher.App.ViewModels;

namespace LabManager.Teacher.App.Views;

public partial class DeploySoftwareWindow : Window
{
    private readonly DeploySoftwareViewModel _viewModel;

    public DeploySoftwareWindow(DeploySoftwareViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.RequestClose += Close;
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string args)
        {
            _viewModel.Arguments = args;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
    }
}
