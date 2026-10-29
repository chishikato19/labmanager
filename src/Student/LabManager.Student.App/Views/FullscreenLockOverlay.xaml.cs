using System.Windows;

namespace LabManager.Student.App.Views;

public partial class FullscreenLockOverlay : Window
{
    public FullscreenLockOverlay()
    {
        InitializeComponent();
        TxtMachineInfo.Text = $"{Environment.MachineName} • {Environment.UserName}";
    }

    public void SetMessage(string message)
    {
        Dispatcher.Invoke(() =>
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                TxtMessage.Text = message;
            }
        });
    }

    public void ShowLock()
    {
        Dispatcher.Invoke(() =>
        {
            Topmost = true;
            Show();
            Activate();
        });
    }

    public void HideLock()
    {
        Dispatcher.Invoke(() =>
        {
            Hide();
        });
    }
}
