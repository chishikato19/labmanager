using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media.Imaging;
using LabManager.Core.Enums;
using LabManager.Core.Models;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;
using LabManager.Teacher.Core.Services;

namespace LabManager.Teacher.App.ViewModels;

public class RemoteControlViewModel : ViewModelBase, IDisposable
{
    private readonly ClassroomManager _manager;
    private readonly string _studentIp;
    private readonly string _studentId;
    private readonly UdpClient _udpInputClient = new();
    private readonly IPEndPoint? _studentUdpEndpoint;
    private BitmapSource? _currentScreen;
    private string _status = "Đang kết nối điều khiển...";
    private long _lastMouseMoveTime;
    private double _lastNormX;
    private double _lastNormY;

    public string MachineName { get; }
    public string IpAddress => _studentIp;

    public BitmapSource? CurrentScreen
    {
        get => _currentScreen;
        private set
        {
            if (SetProperty(ref _currentScreen, value))
            {
                OnPropertyChanged(nameof(HasScreen));
            }
        }
    }

    public bool HasScreen => CurrentScreen != null;

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public bool IsBroadcastingThisStudent => _manager.IsStudentBroadcasting && _manager.BroadcastingStudentId == _studentId;
    public string BroadcastButtonText => IsBroadcastingThisStudent ? "🛑 Dừng chiếu cả lớp" : "📡 Chiếu bài làm cho cả lớp";
    public string BroadcastButtonColor => IsBroadcastingThisStudent ? "#DC2626" : "#E11D48";
    public RelayCommand ToggleBroadcastThisStudentCommand { get; }

    public RemoteControlViewModel(string machineName, string studentIp, ClassroomManager manager, string studentId = "")
    {
        MachineName = machineName;
        _studentIp = studentIp;
        _studentId = studentId;
        _manager = manager;

        ToggleBroadcastThisStudentCommand = new RelayCommand(async () => await ToggleBroadcastThisStudentAsync());

        string cleanStudentIp = _studentIp.StartsWith("::ffff:") ? _studentIp[7..] : _studentIp;
        if (IPAddress.TryParse(cleanStudentIp, out var ip))
        {
            _studentUdpEndpoint = new IPEndPoint(ip, NetworkConfig.DefaultDiscoveryPort); // Port 9000 UDP
        }

        _manager.OnScreenFrameReceived += HandleScreenFrame;
        _ = _manager.SendRemoteSessionControlAsync(_studentIp, true);
        Status = $"Đang điều khiển {MachineName} ({_studentIp})";
    }

    private async Task ToggleBroadcastThisStudentAsync()
    {
        if (IsBroadcastingThisStudent)
        {
            await _manager.StopBroadcastAsync();
        }
        else
        {
            await _manager.StartStudentBroadcastAsync(_studentId, _studentIp, MachineName, fullscreen: true);
        }

        OnPropertyChanged(nameof(IsBroadcastingThisStudent));
        OnPropertyChanged(nameof(BroadcastButtonText));
        OnPropertyChanged(nameof(BroadcastButtonColor));
    }

    private void HandleScreenFrame(ScreenFramePacket packet, string senderIp)
    {
        string cleanSenderIp = senderIp.StartsWith("::ffff:") ? senderIp[7..] : senderIp;
        string cleanStudentIp = _studentIp.StartsWith("::ffff:") ? _studentIp[7..] : _studentIp;

        bool matches = (!string.IsNullOrEmpty(_studentId) && packet.MachineId.Equals(_studentId, StringComparison.OrdinalIgnoreCase))
                       || cleanSenderIp == cleanStudentIp
                       || senderIp == _studentIp
                       || (senderIp == "127.0.0.1" && (_studentIp == "127.0.0.1" || cleanStudentIp == "127.0.0.1"));

        if (!matches) return;

        try
        {
            using var stream = new MemoryStream(packet.ImageBytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = stream;
            bmp.EndInit();
            bmp.Freeze();

            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                CurrentScreen = bmp;
            });
        }
        catch { }
    }

    private void SendInputDirect(RemoteInputPacket packet)
    {
        // 1. Gửi qua UDP tới port 9000 của máy học sinh -> Đạt độ trễ < 1ms, chuột di chuyển tức thì không độ trễ
        if (_studentUdpEndpoint != null)
        {
            try
            {
                byte[] bytes = PacketSerializer.Serialize(CommandType.RemoteInput, packet);
                _ = _udpInputClient.SendAsync(bytes, bytes.Length, _studentUdpEndpoint);
            }
            catch { }
        }

        // 2. Với các thao tác nhấn click chuột hoặc gõ phím, đồng thời gửi TCP làm kênh bảo đảm
        if (packet.InputType != RemoteInputType.MouseMove)
        {
            _ = _manager.SendRemoteInputAsync(_studentIp, packet);
        }
    }

    public void SendMouseMove(double normX, double normY)
    {
        long now = Environment.TickCount64;
        // Tối ưu tần số cập nhật chuột (12ms ~ 80Hz mượt mà như dùng UltraViewer/AnyDesk)
        if (now - _lastMouseMoveTime < 12 && Math.Abs(normX - _lastNormX) < 0.001 && Math.Abs(normY - _lastNormY) < 0.001)
            return;

        _lastMouseMoveTime = now;
        _lastNormX = normX;
        _lastNormY = normY;

        SendInputDirect(new RemoteInputPacket
        {
            InputType = RemoteInputType.MouseMove,
            X = normX,
            Y = normY
        });
    }

    public void SendMouseButton(double normX, double normY, int button, bool isDown)
    {
        SendInputDirect(new RemoteInputPacket
        {
            InputType = isDown ? RemoteInputType.MouseDown : RemoteInputType.MouseUp,
            X = normX,
            Y = normY,
            Button = button
        });
    }

    public void SendMouseWheel(int delta)
    {
        SendInputDirect(new RemoteInputPacket
        {
            InputType = RemoteInputType.MouseWheel,
            WheelDelta = delta
        });
    }

    public void SendKeyEvent(int virtualKey, bool isDown)
    {
        SendInputDirect(new RemoteInputPacket
        {
            InputType = isDown ? RemoteInputType.KeyDown : RemoteInputType.KeyUp,
            VirtualKey = virtualKey
        });
    }

    public void Dispose()
    {
        _manager.OnScreenFrameReceived -= HandleScreenFrame;
        _ = _manager.SendRemoteSessionControlAsync(_studentIp, false);
        _udpInputClient.Dispose();
    }
}
