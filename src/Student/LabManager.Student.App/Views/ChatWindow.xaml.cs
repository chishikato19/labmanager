using System.Windows;
using LabManager.Student.App.ViewModels;

namespace LabManager.Student.App.Views;

public partial class ChatWindow : Window
{
    private bool _isForceClosing;

    public ChatViewModel ViewModel { get; }

    public ChatWindow(ChatViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        Loaded += (_, _) =>
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 20;
            Top = workArea.Bottom - Height - 20;
        };

        ViewModel.OnRequestClose += () =>
        {
            Hide();
        };

        Closing += (s, e) =>
        {
            if (_isForceClosing) return;

            if (!ViewModel.AllowCloseChat)
            {
                e.Cancel = true;
                MessageBox.Show("Giáo viên không cho phép tắt bảng chat lúc này.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            e.Cancel = true;
            Hide();
        };
    }

    public void ForceClose()
    {
        _isForceClosing = true;
        Close();
    }
}
