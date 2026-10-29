using System.Windows;
using LabManager.Student.App.ViewModels;
using LabManager.Student.App.Views;
using LabManager.Student.Core.Services;

namespace LabManager.Student.App;

public partial class App : Application
{
    private StudentCoordinator? _coordinator;
    private FullscreenLockOverlay? _lockOverlay;
    private ScreenBroadcastWindow? _broadcastWindow;
    private ChatWindow? _chatWindow;
    private AnnouncementBannerWindow? _announcementBanner;
    private StudentSubmissionBarWindow? _submissionBar;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _lockOverlay = new FullscreenLockOverlay();
        _broadcastWindow = new ScreenBroadcastWindow();
        _announcementBanner = new AnnouncementBannerWindow();

        _coordinator = new StudentCoordinator();
        _submissionBar = new StudentSubmissionBarWindow(_coordinator);
        _coordinator.OnLockStateChanged += HandleLockStateChanged;
        _coordinator.OnBroadcastStateChanged += HandleBroadcastStateChanged;
        _coordinator.OnBroadcastFrameReceived += bytes => _broadcastWindow?.UpdateFrame(bytes);

        _coordinator.OnFileCollectSessionChanged += session =>
        {
            Dispatcher.Invoke(() =>
            {
                if (session.IsOpen)
                {
                    _submissionBar?.ShowBar(session);
                }
                else
                {
                    _submissionBar?.HideBar();
                }
            });
        };

        var chatVm = new ChatViewModel(_coordinator);
        _chatWindow = new ChatWindow(chatVm);

        _coordinator.OnChatMessageReceived += msg =>
        {
            Dispatcher.Invoke(() =>
            {
                // Lệnh từ giáo viên yêu cầu đóng bảng chat lập tức
                if (msg.CloseChatCommand)
                {
                    _chatWindow?.Hide();
                    return;
                }

                // Nếu là thông báo nổi phía trên màn hình
                if (msg.IsAnnouncement)
                {
                    _chatWindow?.ViewModel.ReceiveMessage(msg);
                    _announcementBanner?.ShowAnnouncement(msg.SenderName, msg.Content, msg.DurationSeconds, msg.SentAt);
                }
                else
                {
                    // Tin nhắn chat trao đổi thông thường
                    if (_chatWindow != null)
                    {
                        _chatWindow.ViewModel.ReceiveMessage(msg);
                        _chatWindow.Show();
                        _chatWindow.Activate();
                    }
                }
            });
        };

        _coordinator.Start();
    }

    private void HandleLockStateChanged(bool isLocked, string message)
    {
        Dispatcher.Invoke(() =>
        {
            if (isLocked)
            {
                _lockOverlay?.SetMessage(message);
                _lockOverlay?.ShowLock();
            }
            else
            {
                _lockOverlay?.HideLock();
            }
        });
    }

    private void HandleBroadcastStateChanged(bool isBroadcasting, bool fullscreen, string message)
    {
        Dispatcher.Invoke(() =>
        {
            if (isBroadcasting)
            {
                _broadcastWindow?.ShowBroadcast(fullscreen, message);
            }
            else
            {
                _broadcastWindow?.HideBroadcast();
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _coordinator?.Dispose();
        _lockOverlay?.Close();
        _broadcastWindow?.Close();
        _announcementBanner?.ForceClose();
        _chatWindow?.ForceClose();
        _submissionBar?.ForceClose();
        base.OnExit(e);
    }
}
