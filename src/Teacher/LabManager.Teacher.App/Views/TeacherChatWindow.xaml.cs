using System.Windows;
using LabManager.Teacher.App.ViewModels;

namespace LabManager.Teacher.App.Views;

public partial class TeacherChatWindow : Window
{
    public TeacherChatViewModel ViewModel { get; }

    public TeacherChatWindow(TeacherChatViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }
}
