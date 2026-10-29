using System.Windows;
using LabManager.Teacher.App.ViewModels;

namespace LabManager.Teacher.App.Views;

public partial class ProcessListWindow : Window
{
    public ProcessListViewModel ViewModel { get; }

    public ProcessListWindow(ProcessListViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }
}
