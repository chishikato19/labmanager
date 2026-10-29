using LabManager.Core.Enums;
using LabManager.Core.Models;
using LabManager.Native.Screen;
using LabManager.Native.System;
using LabManager.Network.Discovery;
using LabManager.Network.Streaming;
using LabManager.Network.Tcp;
using LabManager.Protocol;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;
using LabManager.Student.Core.Handlers;

namespace LabManager.Student.Core.Services;

public class StudentCoordinator : IDisposable
{
    private readonly StudentDiscoveryListener _discoveryListener;
    private readonly TcpCommandListener _commandListener;
    private readonly LockMachineHandler _lockHandler;
    private readonly GdiScreenCapturer _screenCapturer;
    private readonly StudentScreenSender _screenSender;
    private readonly ScreenBroadcastReceiver _broadcastReceiver;
    private readonly FileExchangeHandler _fileHandler = new();
    private readonly PowerControlHandler _powerHandler = new();
    private readonly ProcessControlHandler _processHandler;
    private readonly WebFilterHandler _webHandler;
    private readonly ChatHandler _chatHandler = new();
    private readonly RemoteControlHandler _remoteControlHandler = new();
    private readonly AppUpdateHandler _updateHandler = new();
    private readonly WallpaperHandler _wallpaperHandler = new();
    private readonly DeploySoftwareHandler _deployHandler = new();
    private readonly string _machineId = Guid.NewGuid().ToString("N");
    private string? _currentTeacherIp;
    private bool _isDisposed;

    public event Action<string, string>? OnConnectedToTeacher;
    public event Action<bool, string>? OnLockStateChanged;
    public event Action<bool, bool, string>? OnBroadcastStateChanged;
    public event Action<byte[]>? OnBroadcastFrameReceived;
    public event Action<ChatMessagePacket>? OnChatMessageReceived;
    public event Action<FileCollectRequestPacket>? OnFileCollectSessionChanged;

    public FileCollectRequestPacket? ActiveCollectSession { get; private set; }

    public bool IsLocked => _lockHandler.IsLocked;
    public PowerControlHandler PowerHandler => _powerHandler;
    public ProcessControlHandler ProcessHandler => _processHandler;
    public WebFilterHandler WebHandler => _webHandler;
    public ChatHandler ChatHandler => _chatHandler;

    public StudentCoordinator()
    {
        _chatHandler.OnMessageReceived += msg => OnChatMessageReceived?.Invoke(msg);

        _deployHandler.OnStatusChanged += async status =>
        {
            await SendToTeacherAsync(CommandType.DeploySoftwareStatus, status);
        };

        _processHandler = new ProcessControlHandler(_machineId);
        _processHandler.OnViolationDetected += async packet => await SendToTeacherAsync(CommandType.ProcessViolation, packet);
        _processHandler.OnProcessListReady += async packet => await SendToTeacherAsync(CommandType.ProcessListResponse, packet);

        _webHandler = new WebFilterHandler(_machineId);
        _webHandler.OnWebViolationDetected += async packet => await SendToTeacherAsync(CommandType.WebViolation, packet);
        _webHandler.OnWebActivityReady += async packet => await SendToTeacherAsync(CommandType.WebActivityReport, packet);

        _screenCapturer = new GdiScreenCapturer();
        _screenSender = new StudentScreenSender(_screenCapturer, _machineId);
        _lockHandler = new LockMachineHandler();
        _lockHandler.OnLockStateChanged += (locked, msg) => OnLockStateChanged?.Invoke(locked, msg);

        _broadcastReceiver = new ScreenBroadcastReceiver();
        _broadcastReceiver.OnFrameReceived += frame => OnBroadcastFrameReceived?.Invoke(frame);

        _commandListener = new TcpCommandListener();
        _commandListener.OnCommandReceived += HandleCommandReceivedAsync;

        _discoveryListener = new StudentDiscoveryListener
        {
            ResponsePacketFactory = CreateResponsePacket
        };

        _discoveryListener.OnTeacherBeaconReceived += (beacon, teacherIp) =>
        {
            OnConnectedToTeacher?.Invoke(beacon.TeacherName, teacherIp);

            // Tự động kích hoạt stream màn hình về máy giáo viên
            if (_currentTeacherIp != teacherIp || !_screenSender.IsStreaming)
            {
                _currentTeacherIp = teacherIp;
                _screenSender.Start(teacherIp, NetworkConfig.DefaultStreamPort, targetFps: 2);
            }
        };

        // Nhận lệnh điều khiển chuột/phím siêu tốc qua UDP (< 1ms, không độ trễ TCP)
        _discoveryListener.OnRemoteInputReceived += packet => _remoteControlHandler.HandleInput(packet);

        // Nhận tín hiệu kích hoạt luồng Live nét căng khi giáo viên chỉ chuột vào
        _discoveryListener.OnRemoteSessionReceived += sessionCmd => HandleRemoteSession(sessionCmd);
    }

    public void Start()
    {
        _discoveryListener.Start();
        _commandListener.Start();
    }

    public void Stop()
    {
        _discoveryListener.Stop();
        _commandListener.Stop();
        _screenSender.Stop();
        _broadcastReceiver.Stop();
        _lockHandler.Dispose();
    }

    private Task HandleCommandReceivedAsync(PacketHeader header, byte[] payload)
    {
        if (header.Type == CommandType.LockControl)
        {
            var lockCmd = PacketSerializer.DeserializePayload<LockControlPacket>(payload, 0, payload.Length);
            if (lockCmd != null)
            {
                return _lockHandler.HandleAsync(lockCmd);
            }
        }
        else if (header.Type == CommandType.BroadcastControl)
        {
            var broadcastCmd = PacketSerializer.DeserializePayload<BroadcastControlPacket>(payload, 0, payload.Length);
            if (broadcastCmd != null)
            {
                // Nếu lệnh chiếu màn hình này loại trừ máy hiện tại (vì máy này đang là người phát bài làm)
                if (broadcastCmd.IsBroadcasting && !string.IsNullOrWhiteSpace(broadcastCmd.ExcludedMachineId) 
                    && broadcastCmd.ExcludedMachineId.Equals(_machineId, StringComparison.OrdinalIgnoreCase))
                {
                    _broadcastReceiver.Stop();
                    OnBroadcastStateChanged?.Invoke(false, false, string.Empty);
                    return Task.CompletedTask;
                }

                if (broadcastCmd.IsBroadcasting)
                {
                    _broadcastReceiver.Start(broadcastCmd.MulticastGroup, broadcastCmd.MulticastPort);
                    OnBroadcastStateChanged?.Invoke(true, broadcastCmd.Fullscreen, broadcastCmd.Message);
                }
                else
                {
                    _broadcastReceiver.Stop();
                    OnBroadcastStateChanged?.Invoke(false, false, string.Empty);
                }
            }
        }
        else if (header.Type == CommandType.FileDistribute)
        {
            var distributeCmd = PacketSerializer.DeserializePayload<FileDistributePacket>(payload, 0, payload.Length);
            if (distributeCmd != null)
            {
                return _fileHandler.HandleDistributeAsync(distributeCmd);
            }
        }
        else if (header.Type == CommandType.FileCollectRequest)
        {
            var collectCmd = PacketSerializer.DeserializePayload<FileCollectRequestPacket>(payload, 0, payload.Length);
            if (collectCmd != null)
            {
                if (collectCmd.IsOpen)
                {
                    ActiveCollectSession = collectCmd;
                    _currentTeacherIp = collectCmd.TeacherIp;
                }
                else
                {
                    ActiveCollectSession = null;
                }

                OnFileCollectSessionChanged?.Invoke(collectCmd);

                if (collectCmd.AutoCollectDesktop)
                {
                    return _fileHandler.HandleCollectRequestAsync(collectCmd, _machineId);
                }
                return Task.FromResult(true);
            }
        }
        else if (header.Type == CommandType.PowerControl)
        {
            var powerCmd = PacketSerializer.DeserializePayload<PowerControlPacket>(payload, 0, payload.Length);
            if (powerCmd != null)
            {
                return _powerHandler.HandleAsync(powerCmd);
            }
        }
        else if (header.Type == CommandType.ProcessControl)
        {
            var procCmd = PacketSerializer.DeserializePayload<ProcessControlPacket>(payload, 0, payload.Length);
            if (procCmd != null)
            {
                return _processHandler.HandleAsync(procCmd);
            }
        }
        else if (header.Type == CommandType.WebFilterControl)
        {
            var webCmd = PacketSerializer.DeserializePayload<WebFilterControlPacket>(payload, 0, payload.Length);
            if (webCmd != null)
            {
                return _webHandler.HandleAsync(webCmd);
            }
        }
        else if (header.Type == CommandType.ChatMessage)
        {
            var chatCmd = PacketSerializer.DeserializePayload<ChatMessagePacket>(payload, 0, payload.Length);
            if (chatCmd != null)
            {
                return _chatHandler.HandleAsync(chatCmd);
            }
        }
        else if (header.Type == CommandType.RemoteSession)
        {
            var sessionCmd = PacketSerializer.DeserializePayload<RemoteSessionPacket>(payload, 0, payload.Length);
            if (sessionCmd != null)
            {
                HandleRemoteSession(sessionCmd);
            }
        }
        else if (header.Type == CommandType.RemoteInput)
        {
            var inputCmd = PacketSerializer.DeserializePayload<RemoteInputPacket>(payload, 0, payload.Length);
            if (inputCmd != null)
            {
                _remoteControlHandler.HandleInput(inputCmd);
            }
        }
        else if (header.Type == CommandType.AppUpdate)
        {
            var updateCmd = PacketSerializer.DeserializePayload<AppUpdatePacket>(payload, 0, payload.Length);
            if (updateCmd != null)
            {
                return _updateHandler.HandleUpdateAsync(updateCmd);
            }
        }
        else if (header.Type == CommandType.OpenWebUrl)
        {
            var webCmd = PacketSerializer.DeserializePayload<OpenWebUrlPacket>(payload, 0, payload.Length);
            if (webCmd != null && !string.IsNullOrWhiteSpace(webCmd.Url))
            {
                _ = Task.Run(() => BrowserLauncherHelper.OpenUrl(webCmd.Url, webCmd.ForceChrome));
            }
        }
        else if (header.Type == CommandType.LaunchApp)
        {
            var appCmd = PacketSerializer.DeserializePayload<LaunchAppPacket>(payload, 0, payload.Length);
            if (appCmd != null && !string.IsNullOrWhiteSpace(appCmd.AppNameOrPath))
            {
                _ = Task.Run(() => AppLauncherHelper.LaunchApp(appCmd.AppNameOrPath, appCmd.Arguments));
            }
        }
        else if (header.Type == CommandType.SetWallpaper)
        {
            var wallpaperCmd = PacketSerializer.DeserializePayload<SetWallpaperPacket>(payload, 0, payload.Length);
            if (wallpaperCmd != null)
            {
                return _wallpaperHandler.HandleWallpaperAsync(wallpaperCmd);
            }
        }
        else if (header.Type == CommandType.DeploySoftware)
        {
            var deployCmd = PacketSerializer.DeserializePayload<DeploySoftwarePacket>(payload, 0, payload.Length);
            if (deployCmd != null)
            {
                _ = Task.Run(() => _deployHandler.HandleDeployAsync(deployCmd, _machineId));
            }
        }

        return Task.CompletedTask;
    }

    private void HandleRemoteSession(RemoteSessionPacket sessionCmd)
    {
        if ((sessionCmd.Start || sessionCmd.HighQualityFocus) && !string.IsNullOrWhiteSpace(sessionCmd.TeacherIp))
        {
            _currentTeacherIp = sessionCmd.TeacherIp;
            _screenSender.Start(_currentTeacherIp, NetworkConfig.DefaultStreamPort);
        }

        if (sessionCmd.Start)
        {
            _screenSender.SetRemoteControlMode(true);
        }
        else if (sessionCmd.HighQualityFocus)
        {
            _screenSender.SetFocusMode(true);
        }
        else
        {
            _screenSender.SetRemoteControlMode(false);
            _screenSender.SetFocusMode(false);
        }
    }

    public async Task SendChatMessageAsync(string content)
    {
        var packet = new ChatMessagePacket
        {
            SenderId = _machineId,
            SenderName = $"{Environment.MachineName} ({Environment.UserName})",
            Content = content,
            IsBroadcast = false
        };
        await SendToTeacherAsync(CommandType.ChatMessage, packet);
    }

    private async Task SendToTeacherAsync<T>(CommandType type, T packet)
    {
        if (string.IsNullOrWhiteSpace(_currentTeacherIp)) return;
        try
        {
            byte[] bytes = PacketSerializer.Serialize(type, packet);
            using var client = new System.Net.Sockets.TcpClient();
            using var cts = new CancellationTokenSource(3000);
            await client.ConnectAsync(_currentTeacherIp, NetworkConfig.DefaultEventPort, cts.Token);
            using var stream = client.GetStream();
            await stream.WriteAsync(bytes, cts.Token);
        }
        catch
        {
            // Bỏ qua lỗi kết nối tạm thời
        }
    }

    public async Task<bool> SubmitFilesToTeacherAsync(
        string studentName,
        string note,
        IReadOnlyList<string> filePaths,
        IProgress<(int current, int total, string fileName)>? progress = null)
    {
        string? targetIp = ActiveCollectSession?.TeacherIp ?? _currentTeacherIp;
        if (string.IsNullOrWhiteSpace(targetIp))
        {
            return false;
        }

        int port = ActiveCollectSession?.TeacherFilePort ?? NetworkConfig.DefaultFilePort;
        return await _fileHandler.SubmitFilesAsync(targetIp, port, _machineId, studentName, note, filePaths, progress);
    }

    private DiscoveryResponsePacket CreateResponsePacket()
    {
        var (ip, mac) = NetworkInfoHelper.GetActiveNetworkInfo();

        return new DiscoveryResponsePacket
        {
            MachineId = _machineId,
            MachineName = Environment.MachineName,
            CurrentUser = Environment.UserName,
            MacAddress = mac,
            CommandPort = NetworkConfig.DefaultCommandPort,
            Status = _lockHandler.IsLocked ? MachineStatus.Locked : MachineStatus.Online,
            OsVersion = Environment.OSVersion.VersionString,
            AppVersion = typeof(StudentCoordinator).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"
        };
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Stop();
        _webHandler.Dispose();
        _processHandler.Dispose();
        _discoveryListener.Dispose();
        _commandListener.Dispose();
        _screenSender.Dispose();
        _broadcastReceiver.Dispose();
        _screenCapturer.Dispose();
        _lockHandler.Dispose();
    }
}
