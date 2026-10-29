using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace LabManager.Student.App.Views;

public partial class AnnouncementBannerWindow : Window
{
    private readonly DispatcherTimer _timer;
    private double _totalSeconds = 10;
    private double _remainingSeconds = 10;
    private bool _canClose;

    public AnnouncementBannerWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += Timer_Tick;

        PreviewKeyDown += (_, e) =>
        {
            // Không cho phép học sinh dùng phím tắt để tắt thông báo
            if (e.Key == Key.System || e.Key == Key.F4 || e.Key == Key.Escape)
            {
                e.Handled = true;
            }
        };

        Closing += (_, e) =>
        {
            if (!_canClose)
            {
                e.Cancel = true;
            }
        };
    }

    public void ShowAnnouncement(string senderName, string content, int durationSeconds, DateTime? sentAt = null)
    {
        _totalSeconds = durationSeconds > 0 ? durationSeconds : 10;
        _remainingSeconds = _totalSeconds;

        TxtSender.Text = string.IsNullOrWhiteSpace(senderName) ? "Giáo viên" : senderName;
        TxtContent.Text = content;
        var time = sentAt ?? DateTime.UtcNow;
        TxtTime.Text = $" ({time.ToLocalTime():HH:mm})";

        TxtCountdown.Text = $"{Math.Ceiling(_remainingSeconds)}s";
        ProgCountdown.Value = 100;

        _canClose = false;

        // Định vị nổi ở phía trên cùng màn hình
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + 16;

        Show();
        Activate();
        Topmost = true;

        _timer.Stop();
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _remainingSeconds -= 0.1;

        if (_remainingSeconds <= 0)
        {
            _timer.Stop();
            _canClose = true;
            Hide();
            return;
        }

        TxtCountdown.Text = $"{Math.Ceiling(_remainingSeconds)}s";
        if (_totalSeconds > 0)
        {
            ProgCountdown.Value = Math.Max(0, (_remainingSeconds / _totalSeconds) * 100);
        }
    }

    public void ForceClose()
    {
        _timer.Stop();
        _canClose = true;
        Close();
    }
}
