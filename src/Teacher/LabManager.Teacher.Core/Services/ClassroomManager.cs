using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
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

namespace LabManager.Teacher.Core.Services;

public class ClassroomManager : IDisposable
{
    private readonly TeacherDiscoveryService _discoveryService;
    private readonly TeacherScreenReceiver _screenReceiver;
    private readonly TcpCommandListener _eventListener = new();
    private readonly TeacherCommandSender _commandSender = new();
    private readonly TeacherFileService _fileService = new();
    private readonly LanUpdateServer _updateServer = new();
    private readonly LanDeployServer _deployServer = new();
    private readonly System.Net.Sockets.UdpClient _udpQuickSender = new();
    private readonly ConcurrentDictionary<string, StudentNode> _students = new();
    private readonly ConcurrentDictionary<string, bool> _activeRemoteSessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _healthCheckTimer;
    private ScreenBroadcastSender? _broadcastSender;
    private bool _laserPointerEnabled;
    private string _lastTeacherIp = string.Empty;
    private bool _isDisposed;

    public LanUpdateServer UpdateServer => _updateServer;
    public LanDeployServer DeployServer => _deployServer;

    public bool IsBroadcasting => (_broadcastSender?.IsBroadcasting ?? false) || IsStudentBroadcasting;
    public bool IsStudentBroadcasting { get; private set; }
    public string? BroadcastingStudentId { get; private set; }
    public string? BroadcastingStudentName { get; private set; }
    private string? _broadcastingStudentIp;

    public bool LaserPointerEnabled
    {
        get => _broadcastSender?.LaserPointerEnabled ?? _laserPointerEnabled;
        set
        {
            _laserPointerEnabled = value;
            if (_broadcastSender != null)
                _broadcastSender.LaserPointerEnabled = value;
        }
    }
    public string CurrentCollectionFolder => _fileService.CurrentCollectionFolder;
    public bool IsCollectingFiles => _fileService.IsCollecting;

    public event Action<StudentNode>? OnStudentJoined;
    public event Action<StudentNode>? OnStudentUpdated;
    public event Action<StudentNode>? OnStudentOffline;
    public event Action<ScreenFramePacket, string>? OnScreenFrameReceived;
    public event Action<FileCollectResponsePacket, string>? OnFileCollected;
    public event Action<ProcessViolationPacket, string>? OnProcessViolation;
    public event Action<ProcessListResponsePacket, string>? OnProcessListReceived;
    public event Action<WebViolationPacket, string>? OnWebViolation;
    public event Action<WebActivityReportPacket, string>? OnWebActivityReceived;
    public event Action<ChatMessagePacket, string>? OnChatMessageReceived;
    public event Action<DeploySoftwareStatusPacket, string>? OnDeploySoftwareStatusReceived;

    private int _screenIntervalSeconds = 3;
    public int ScreenIntervalSeconds
    {
        get => _screenIntervalSeconds;
        set
        {
            _screenIntervalSeconds = value;
            _discoveryService.ScreenIntervalSeconds = value;
            OfflineThreshold = TimeSpan.FromSeconds(Math.Max(15, value * 3));
        }
    }

    public TimeSpan OfflineThreshold { get; set; } = TimeSpan.FromSeconds(15);

    public ClassroomManager()
    {
        _discoveryService = new TeacherDiscoveryService();
        _discoveryService.OnStudentDiscovered += HandleStudentDiscovered;

        _screenReceiver = new TeacherScreenReceiver();
        _screenReceiver.OnFrameReceived += HandleScreenFrame;

        _fileService.OnFileCollected += (submission, path) => OnFileCollected?.Invoke(submission, path);

        _eventListener.OnCommandReceived += HandleEventReceivedAsync;

        // Kiểm tra tình trạng online/offline định kỳ mỗi 2 giây
        _healthCheckTimer = new Timer(CheckOfflineStudents, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    public void StartDiscovery(string teacherName = "Teacher", string roomName = "Phòng máy")
    {
        _discoveryService.ScreenIntervalSeconds = _screenIntervalSeconds;
        _discoveryService.Start(teacherName, roomName);
        _screenReceiver.Start();
        _eventListener.Start(NetworkConfig.DefaultEventPort);
    }

    public void StopDiscovery()
    {
        _discoveryService.Stop();
        _screenReceiver.Stop();
        _eventListener.Stop();
    }

    public IReadOnlyCollection<StudentNode> GetAllStudents()
    {
        return _students.Values.ToList().AsReadOnly();
    }

    public async Task SetScreenIntervalAllAsync(int seconds)
    {
        ScreenIntervalSeconds = seconds;
        _ = _discoveryService.BroadcastBeaconNowAsync();

        var packet = new ScreenIntervalConfigPacket { IntervalSeconds = seconds };
        byte[] bytes = PacketSerializer.Serialize(CommandType.ScreenIntervalConfig, packet);
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);

        await _commandSender.SendToMultipleAsync(onlineIps, bytes);
    }

    public async Task<bool> SendLockCommandAsync(string studentIp, bool lockScreen, bool blockInput, string message = "Máy tính đang bị khóa bởi giáo viên.")
    {
        var packet = new LockControlPacket
        {
            LockScreen = lockScreen,
            BlockInput = blockInput,
            Message = message
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.LockControl, packet);
        return await _commandSender.SendCommandAsync(studentIp, bytes);
    }

    public async Task SendLockAllAsync(bool lockScreen, bool blockInput, string message = "Máy tính đang bị khóa bởi giáo viên.")
    {
        var packet = new LockControlPacket
        {
            LockScreen = lockScreen,
            BlockInput = blockInput,
            Message = message
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.LockControl, packet);
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);

        await _commandSender.SendToMultipleAsync(onlineIps, bytes);
    }

    public async Task<bool> SendPowerCommandAsync(string studentIp, PowerAction action, bool force = true, int delaySeconds = 0)
    {
        var packet = new PowerControlPacket
        {
            Action = action,
            Force = force,
            DelaySeconds = delaySeconds
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.PowerControl, packet);
        return await _commandSender.SendCommandAsync(studentIp, bytes);
    }

    public async Task SendPowerAllAsync(PowerAction action, bool force = true, int delaySeconds = 0)
    {
        var packet = new PowerControlPacket
        {
            Action = action,
            Force = force,
            DelaySeconds = delaySeconds
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.PowerControl, packet);
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);

        await _commandSender.SendToMultipleAsync(onlineIps, bytes);
    }

    public async Task WakeOnLanAsync(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress)) return;
        await WakeOnLanHelper.SendWakeOnLanAsync(macAddress);
    }

    public async Task WakeOnLanAllAsync()
    {
        var tasks = _students.Values
            .Where(s => !string.IsNullOrWhiteSpace(s.MacAddress))
            .Select(s => WakeOnLanHelper.SendWakeOnLanAsync(s.MacAddress));

        await Task.WhenAll(tasks);
    }

    public async Task StartBroadcastAsync(bool fullscreen = true)
    {
        if (IsBroadcasting)
        {
            await StopBroadcastAsync();
        }

        _broadcastSender ??= new ScreenBroadcastSender(new GdiScreenCapturer());
        _broadcastSender.LaserPointerEnabled = _laserPointerEnabled;
        _broadcastSender.Start(NetworkConfig.DefaultMulticastGroup, NetworkConfig.DefaultMulticastPort, targetFps: 15);

        var packet = new BroadcastControlPacket
        {
            IsBroadcasting = true,
            MulticastGroup = NetworkConfig.DefaultMulticastGroup,
            MulticastPort = NetworkConfig.DefaultMulticastPort,
            Fullscreen = fullscreen,
            Message = "Giáo viên đang chiếu màn hình bài giảng."
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.BroadcastControl, packet);
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);

        await _commandSender.SendToMultipleAsync(onlineIps, bytes);
    }

    public async Task StartStudentBroadcastAsync(string studentId, string studentIp, string presenterName, bool fullscreen = true)
    {
        if (IsBroadcasting)
        {
            await StopBroadcastAsync();
        }

        IsStudentBroadcasting = true;
        BroadcastingStudentId = studentId;
        BroadcastingStudentName = presenterName;
        _broadcastingStudentIp = studentIp;

        // 1. Kích hoạt chế độ Live nét căng Native 20 FPS trên máy học sinh này
        SendFocusStream(studentIp, true);

        // 2. Bật Multicast Sender ở chế độ Relay khung hình học sinh
        _broadcastSender ??= new ScreenBroadcastSender(new GdiScreenCapturer());
        _broadcastSender.StartRelayMode(NetworkConfig.DefaultMulticastGroup, NetworkConfig.DefaultMulticastPort);

        // 3. Gửi lệnh chiếu màn hình cho toàn bộ học sinh KHÁC (loại trừ máy học sinh đang phát để không che màn hình của em đó)
        string cleanStudentIp = studentIp.StartsWith("::ffff:") ? studentIp[7..] : studentIp;
        var targetIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline 
                     && !string.IsNullOrWhiteSpace(s.IpAddress) 
                     && s.Id != studentId
                     && s.IpAddress != cleanStudentIp
                     && s.IpAddress != studentIp)
            .Select(s => s.IpAddress);

        var packet = new BroadcastControlPacket
        {
            IsBroadcasting = true,
            MulticastGroup = NetworkConfig.DefaultMulticastGroup,
            MulticastPort = NetworkConfig.DefaultMulticastPort,
            Fullscreen = fullscreen,
            Message = $"Đang theo dõi bài làm của học sinh: {presenterName}",
            PresenterName = presenterName,
            ExcludedMachineId = studentId
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.BroadcastControl, packet);
        await _commandSender.SendToMultipleAsync(targetIps, bytes);
    }

    public async Task StopBroadcastAsync()
    {
        if (IsStudentBroadcasting && !string.IsNullOrWhiteSpace(_broadcastingStudentIp))
        {
            SendFocusStream(_broadcastingStudentIp, false);
        }

        IsStudentBroadcasting = false;
        BroadcastingStudentId = null;
        BroadcastingStudentName = null;
        _broadcastingStudentIp = null;

        _broadcastSender?.Stop();

        var packet = new BroadcastControlPacket
        {
            IsBroadcasting = false
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.BroadcastControl, packet);
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);

        await _commandSender.SendToMultipleAsync(onlineIps, bytes);
    }

    public async Task<int> DistributeFileAsync(string filePath, IEnumerable<string>? targetIps = null, string targetSubFolder = "BaiTap", bool autoExtract = true)
    {
        var ips = targetIps ?? _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress);

        return await _fileService.DistributeFileAsync(ips, filePath, targetSubFolder, autoExtract);
    }

    public async Task<int> SendWallpaperAsync(string imagePath, int style = 10, IEnumerable<string>? targetIps = null)
    {
        if (!File.Exists(imagePath)) return 0;

        byte[] imageBytes = await File.ReadAllBytesAsync(imagePath);
        var packet = new SetWallpaperPacket
        {
            FileName = Path.GetFileName(imagePath),
            ImageBytes = imageBytes,
            Style = style
        };

        byte[] packetBytes = PacketSerializer.Serialize(CommandType.SetWallpaper, packet);
        var ips = targetIps?.ToList() ?? _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress)
            .ToList();

        if (ips.Count == 0) return 0;

        var results = await _commandSender.SendToMultipleAsync(ips, packetBytes);
        return results.Values.Count(success => success);
    }

    public async Task<int> StartSoftwareDeployAsync(string installerPath, string arguments, IEnumerable<string>? targetIps = null)
    {
        if (!File.Exists(installerPath)) return 0;

        // 1. Khởi động LanDeployServer phát tệp
        _deployServer.Start(installerPath);

        // 2. Tính SHA256 và tạo đường dẫn tải qua HTTP LAN
        string sha256 = await LanDeployServer.ComputeFileSha256Async(installerPath);
        var (localIp, _) = NetworkInfoHelper.GetActiveNetworkInfo();
        string downloadUrl = $"http://{localIp}:{_deployServer.Port}/{Path.GetFileName(installerPath)}";

        bool isMsi = installerPath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);

        var packet = new DeploySoftwarePacket
        {
            DownloadUrl = downloadUrl,
            FileName = Path.GetFileName(installerPath),
            Arguments = arguments,
            Sha256 = sha256,
            IsMsi = isMsi
        };

        byte[] packetBytes = PacketSerializer.Serialize(CommandType.DeploySoftware, packet);
        var ips = targetIps?.ToList() ?? _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress)
            .ToList();

        if (ips.Count == 0) return 0;

        var results = await _commandSender.SendToMultipleAsync(ips, packetBytes);
        return results.Values.Count(success => success);
    }

    public async Task StartCollectFilesSessionAsync(
        string teacherIp,
        string folderPath,
        string sessionTitle = "Nộp bài cho Giáo viên",
        bool prefixMachineName = false,
        bool separateMachineFolder = false)
    {
        _lastTeacherIp = teacherIp;
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);

        await _fileService.StartCollectionSessionAsync(onlineIps, teacherIp, folderPath, sessionTitle, prefixMachineName, separateMachineFolder);
    }

    public async Task StopCollectFilesSessionAsync(string teacherIp)
    {
        _lastTeacherIp = teacherIp;
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);

        await _fileService.StopCollectionSessionAsync(onlineIps, teacherIp);
    }

    public async Task RequestCollectAllAsync(string teacherIp, string sourceSubFolder = "BaiTap")
    {
        _lastTeacherIp = teacherIp;
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);

        await _fileService.RequestCollectAllAsync(onlineIps, teacherIp, sourceSubFolder);
    }

    public async Task<bool> RequestProcessListAsync(string studentIp)
    {
        var packet = new ProcessControlPacket
        {
            Action = ProcessAction.GetRunningProcesses
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.ProcessControl, packet);
        return await _commandSender.SendCommandAsync(studentIp, bytes);
    }

    public async Task<bool> SendKillProcessAsync(string studentIp, string processName, int pid = 0)
    {
        var packet = new ProcessControlPacket
        {
            Action = ProcessAction.KillProcess,
            TargetPid = pid,
            TargetProcessName = processName
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.ProcessControl, packet);
        return await _commandSender.SendCommandAsync(studentIp, bytes);
    }

    public async Task SendSetBlacklistAllAsync(IEnumerable<string> blacklistedProcesses)
    {
        var packet = new ProcessControlPacket
        {
            Action = ProcessAction.SetBlacklist,
            Blacklist = blacklistedProcesses.ToList()
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.ProcessControl, packet);
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);

        await _commandSender.SendToMultipleAsync(onlineIps, bytes);
    }

    public async Task SendClearBlacklistAllAsync()
    {
        var packet = new ProcessControlPacket
        {
            Action = ProcessAction.ClearBlacklist
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.ProcessControl, packet);
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);

        await _commandSender.SendToMultipleAsync(onlineIps, bytes);
    }

    public async Task SendSetWebBlacklistAllAsync(IEnumerable<string> domains)
    {
        var packet = new WebFilterControlPacket
        {
            Action = WebFilterAction.SetDomainBlacklist,
            BlacklistedDomains = domains.ToList()
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.WebFilterControl, packet);
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);

        await _commandSender.SendToMultipleAsync(onlineIps, bytes);
    }

    public async Task SendClearWebBlacklistAllAsync()
    {
        var packet = new WebFilterControlPacket
        {
            Action = WebFilterAction.ClearDomainBlacklist
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.WebFilterControl, packet);
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);

        await _commandSender.SendToMultipleAsync(onlineIps, bytes);
    }

    public async Task<bool> SendOpenUrlAsync(string studentIp, string url, bool forceChrome = true)
    {
        var packet = new OpenWebUrlPacket
        {
            Url = url,
            ForceChrome = forceChrome
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.OpenWebUrl, packet);
        return await _commandSender.SendCommandAsync(studentIp, bytes);
    }

    public async Task SendOpenUrlToTargetsAsync(IEnumerable<string> targetIps, string url, bool forceChrome = true)
    {
        var packet = new OpenWebUrlPacket
        {
            Url = url,
            ForceChrome = forceChrome
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.OpenWebUrl, packet);
        await _commandSender.SendToMultipleAsync(targetIps, bytes);
    }

    public async Task SendOpenUrlAllAsync(string url, bool forceChrome = true)
    {
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);
        await SendOpenUrlToTargetsAsync(onlineIps, url, forceChrome);
    }

    public async Task<bool> SendLaunchAppAsync(string studentIp, string appNameOrPath, string arguments = "")
    {
        var packet = new LaunchAppPacket
        {
            AppNameOrPath = appNameOrPath,
            Arguments = arguments
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.LaunchApp, packet);
        return await _commandSender.SendCommandAsync(studentIp, bytes);
    }

    public async Task SendLaunchAppToTargetsAsync(IEnumerable<string> targetIps, string appNameOrPath, string arguments = "")
    {
        var packet = new LaunchAppPacket
        {
            AppNameOrPath = appNameOrPath,
            Arguments = arguments
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.LaunchApp, packet);
        await _commandSender.SendToMultipleAsync(targetIps, bytes);
    }

    public async Task SendLaunchAppAllAsync(string appNameOrPath, string arguments = "")
    {
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);
        await SendLaunchAppToTargetsAsync(onlineIps, appNameOrPath, arguments);
    }

    public async Task SendCleanAndInitDriveToTargetsAsync(IEnumerable<string> targetIps, string driveLetter, List<string> folderNames)
    {
        var packet = new CleanAndInitDrivePacket
        {
            DriveLetter = driveLetter,
            FolderNames = folderNames
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.CleanAndInitDrive, packet);
        await _commandSender.SendToMultipleAsync(targetIps, bytes);
    }

    public async Task SendCleanAndInitDriveAllAsync(string driveLetter, List<string> folderNames)
    {
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);
        await SendCleanAndInitDriveToTargetsAsync(onlineIps, driveLetter, folderNames);
    }

    public async Task<bool> RequestWebActivityAsync(string studentIp)
    {
        var packet = new WebFilterControlPacket
        {
            Action = WebFilterAction.GetWebActivity
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.WebFilterControl, packet);
        return await _commandSender.SendCommandAsync(studentIp, bytes);
    }

    public async Task<bool> SendChatMessageAsync(
        string studentIp, 
        string content, 
        string targetId = "", 
        string targetName = "",
        bool isAnnouncement = false,
        int durationSeconds = 10,
        bool allowCloseChat = true,
        bool allowStudentReply = true)
    {
        var packet = new ChatMessagePacket
        {
            SenderId = "TEACHER",
            SenderName = isAnnouncement ? "Giáo viên (Thông báo)" : "Giáo viên",
            TargetId = targetId,
            TargetName = targetName,
            Content = content,
            IsBroadcast = false,
            IsAnnouncement = isAnnouncement,
            DurationSeconds = durationSeconds > 0 ? durationSeconds : 10,
            AllowCloseChat = allowCloseChat,
            AllowStudentReply = allowStudentReply
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.ChatMessage, packet);
        return await _commandSender.SendCommandAsync(studentIp, bytes);
    }

    public async Task SendBroadcastMessageAsync(
        string content,
        bool isAnnouncement = false,
        int durationSeconds = 10,
        bool allowCloseChat = true,
        bool allowStudentReply = true)
    {
        var packet = new ChatMessagePacket
        {
            SenderId = "TEACHER",
            SenderName = isAnnouncement ? "Giáo viên (Thông báo toàn phòng)" : "Giáo viên (Toàn phòng)",
            TargetId = "ALL",
            TargetName = "Toàn phòng",
            Content = content,
            IsBroadcast = true,
            IsAnnouncement = isAnnouncement,
            DurationSeconds = durationSeconds > 0 ? durationSeconds : 10,
            AllowCloseChat = allowCloseChat,
            AllowStudentReply = allowStudentReply
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.ChatMessage, packet);
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);

        await _commandSender.SendToMultipleAsync(onlineIps, bytes);
    }

    public async Task<bool> CloseStudentChatAsync(string studentIp)
    {
        var packet = new ChatMessagePacket
        {
            SenderId = "TEACHER",
            CloseChatCommand = true
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.ChatMessage, packet);
        return await _commandSender.SendCommandAsync(studentIp, bytes);
    }

    public async Task CloseAllStudentsChatAsync()
    {
        var packet = new ChatMessagePacket
        {
            SenderId = "TEACHER",
            CloseChatCommand = true
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.ChatMessage, packet);
        var onlineIps = _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress.StartsWith("::ffff:") ? s.IpAddress[7..] : s.IpAddress);

        await _commandSender.SendToMultipleAsync(onlineIps, bytes);
    }

    public bool IsStudentInRemoteSession(string studentIp)
    {
        string cleanIp = studentIp.StartsWith("::ffff:") ? studentIp[7..] : studentIp;
        return _activeRemoteSessions.ContainsKey(cleanIp);
    }

    public void SendFocusStream(string studentIp, bool enableFocus)
    {
        if (string.IsNullOrWhiteSpace(studentIp)) return;
        string cleanIp = studentIp.StartsWith("::ffff:") ? studentIp[7..] : studentIp;

        // Nếu máy học sinh đang trong phiên điều khiển từ xa, không cho phép sự kiện hover/unhover làm rớt chất lượng
        if (!enableFocus && _activeRemoteSessions.ContainsKey(cleanIp))
        {
            return;
        }

        if (!System.Net.IPAddress.TryParse(cleanIp, out var ip)) return;

        try
        {
            var (localIp, _) = NetworkInfoHelper.GetActiveNetworkInfo();
            var packet = new RemoteSessionPacket
            {
                Start = false,
                HighQualityFocus = enableFocus,
                IsControlSession = false,
                TeacherIp = localIp
            };
            byte[] bytes = PacketSerializer.Serialize(CommandType.RemoteSession, packet);
            var endpoint = new System.Net.IPEndPoint(ip, NetworkConfig.DefaultDiscoveryPort);
            _ = _udpQuickSender.SendAsync(bytes, bytes.Length, endpoint);
        }
        catch { }
    }

    public async Task<bool> SendRemoteSessionControlAsync(string studentIp, bool start)
    {
        string cleanIp = studentIp.StartsWith("::ffff:") ? studentIp[7..] : studentIp;
        if (start)
        {
            _activeRemoteSessions[cleanIp] = true;
        }
        else
        {
            _activeRemoteSessions.TryRemove(cleanIp, out _);
        }

        var (localIp, _) = NetworkInfoHelper.GetActiveNetworkInfo();
        var packet = new RemoteSessionPacket
        {
            Start = start,
            IsControlSession = true,
            TeacherIp = localIp
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.RemoteSession, packet);

        // Phát UDP trước để máy học sinh chuyển chế độ ngay lập tức (< 1ms)
        if (System.Net.IPAddress.TryParse(cleanIp, out var ip))
        {
            try
            {
                var endpoint = new System.Net.IPEndPoint(ip, NetworkConfig.DefaultDiscoveryPort);
                _ = _udpQuickSender.SendAsync(bytes, bytes.Length, endpoint);
            }
            catch { }
        }

        return await _commandSender.SendCommandAsync(studentIp, bytes);
    }

    public async Task<bool> SendRemoteInputAsync(string studentIp, RemoteInputPacket inputPacket)
    {
        byte[] bytes = PacketSerializer.Serialize(CommandType.RemoteInput, inputPacket);
        return await _commandSender.SendCommandAsync(studentIp, bytes);
    }

    public async Task<int> PushUpdateToStudentsAsync(string setupFilePath, IEnumerable<string>? targetIps = null)
    {
        if (!File.Exists(setupFilePath)) return 0;

        string sha256 = await LanUpdateServer.ComputeFileSha256Async(setupFilePath);
        long fileLength = new FileInfo(setupFilePath).Length;
        string version = "1.0.0";
        try
        {
            var fileVer = System.Diagnostics.FileVersionInfo.GetVersionInfo(setupFilePath);
            if (!string.IsNullOrWhiteSpace(fileVer.FileVersion))
                version = fileVer.FileVersion;
        }
        catch { }

        _updateServer.Start(setupFilePath, NetworkConfig.DefaultUpdateHttpPort);

        var (localIp, _) = NetworkInfoHelper.GetActiveNetworkInfo();

        var packet = new AppUpdatePacket
        {
            Version = version,
            DownloadUrl = $"http://{localIp}:{NetworkConfig.DefaultUpdateHttpPort}/student_setup.exe",
            Sha256 = sha256,
            FileSizeBytes = fileLength
        };

        byte[] packetBytes = PacketSerializer.Serialize(CommandType.AppUpdate, packet);

        var destinations = targetIps?.ToList() ?? _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress)
            .ToList();

        if (destinations.Count == 0) return 0;

        var results = await _commandSender.SendToMultipleAsync(destinations, packetBytes, NetworkConfig.DefaultCommandPort);
        return results.Values.Count(success => success);
    }

    public void StopUpdateServer()
    {
        _updateServer.Stop();
    }

    private Task HandleEventReceivedAsync(PacketHeader header, byte[] payload)
    {
        if (header.Type == CommandType.ProcessViolation)
        {
            var packet = PacketSerializer.DeserializePayload<ProcessViolationPacket>(payload, 0, payload.Length);
            if (packet != null)
            {
                OnProcessViolation?.Invoke(packet, packet.MachineName);
            }
        }
        else if (header.Type == CommandType.ProcessListResponse)
        {
            var packet = PacketSerializer.DeserializePayload<ProcessListResponsePacket>(payload, 0, payload.Length);
            if (packet != null)
            {
                OnProcessListReceived?.Invoke(packet, packet.MachineName);
            }
        }
        else if (header.Type == CommandType.WebViolation)
        {
            var packet = PacketSerializer.DeserializePayload<WebViolationPacket>(payload, 0, payload.Length);
            if (packet != null)
            {
                OnWebViolation?.Invoke(packet, packet.MachineName);
            }
        }
        else if (header.Type == CommandType.WebActivityReport)
        {
            var packet = PacketSerializer.DeserializePayload<WebActivityReportPacket>(payload, 0, payload.Length);
            if (packet != null)
            {
                OnWebActivityReceived?.Invoke(packet, packet.MachineName);
            }
        }
        else if (header.Type == CommandType.ChatMessage)
        {
            var packet = PacketSerializer.DeserializePayload<ChatMessagePacket>(payload, 0, payload.Length);
            if (packet != null)
            {
                OnChatMessageReceived?.Invoke(packet, packet.SenderName);
            }
        }
        else if (header.Type == CommandType.DeploySoftwareStatus)
        {
            var packet = PacketSerializer.DeserializePayload<DeploySoftwareStatusPacket>(payload, 0, payload.Length);
            if (packet != null)
            {
                OnDeploySoftwareStatusReceived?.Invoke(packet, packet.MachineName);
            }
        }

        return Task.CompletedTask;
    }

    private void HandleScreenFrame(ScreenFramePacket packet, string senderIp)
    {
        string cleanSenderIp = senderIp.StartsWith("::ffff:") ? senderIp[7..] : senderIp;
        bool found = false;

        // Cập nhật LastSeen cho student
        foreach (var student in _students.Values)
        {
            if (!string.IsNullOrEmpty(packet.MachineId))
            {
                if (student.Id.Equals(packet.MachineId, StringComparison.OrdinalIgnoreCase))
                {
                    student.LastSeen = DateTime.UtcNow;
                    found = true;
                    break;
                }
            }
            else if (student.IpAddress == cleanSenderIp || student.IpAddress == senderIp)
            {
                student.LastSeen = DateTime.UtcNow;
                found = true;
                break;
            }
        }

        // Tự động yêu cầu máy học sinh nhận diện nếu đang nhận stream mà máy chưa có trong danh sách
        if (!found && IPAddress.TryParse(cleanSenderIp, out var parsedSenderIp))
        {
            _ = _discoveryService.SendBeaconToIpAsync(parsedSenderIp);
        }

        // Nếu đang ở chế độ chiếu bài làm của học sinh này cho cả lớp
        if (IsStudentBroadcasting && !string.IsNullOrEmpty(BroadcastingStudentId))
        {
            string? cleanBroadcastingIp = _broadcastingStudentIp != null && _broadcastingStudentIp.StartsWith("::ffff:") ? _broadcastingStudentIp[7..] : _broadcastingStudentIp;

            bool matches = packet.MachineId.Equals(BroadcastingStudentId, StringComparison.OrdinalIgnoreCase)
                           || cleanSenderIp == cleanBroadcastingIp
                           || senderIp == _broadcastingStudentIp;

            if (matches)
            {
                _ = _broadcastSender?.BroadcastExternalFrameAsync(packet.ImageBytes);
            }
        }

        OnScreenFrameReceived?.Invoke(packet, senderIp);
    }

    private static bool IsValidMac(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return false;
        string clean = mac.Replace(":", "").Replace("-", "").Trim();
        return clean.Length > 0 && !clean.All(c => c == '0');
    }

    private void HandleStudentDiscovered(DiscoveryResponsePacket packet, string ipAddress)
    {
        string cleanIp = ipAddress.StartsWith("::ffff:") ? ipAddress[7..] : ipAddress;

        // Định danh duy nhất: Ưu tiên MachineId, sau đó tới MAC hợp lệ (khác 0), cuối cùng là Tên máy + IP
        string key = !string.IsNullOrWhiteSpace(packet.MachineId)
            ? packet.MachineId
            : (IsValidMac(packet.MacAddress) ? packet.MacAddress : $"{packet.MachineName}_{cleanIp}");

        if (_students.TryGetValue(key, out var existing))
        {
            bool wasOffline = existing.Status == MachineStatus.Offline;

            existing.IpAddress = cleanIp;
            existing.MachineName = packet.MachineName;
            existing.CurrentUser = packet.CurrentUser;
            existing.CommandPort = packet.CommandPort;
            existing.Status = packet.Status;
            existing.AppVersion = packet.AppVersion;
            if (IsValidMac(packet.MacAddress))
            {
                existing.MacAddress = packet.MacAddress;
            }
            existing.LastSeen = DateTime.UtcNow;

            if (wasOffline)
            {
                OnStudentJoined?.Invoke(existing);
                if (_fileService.IsCollecting && !string.IsNullOrWhiteSpace(_lastTeacherIp))
                {
                    _ = _fileService.StartCollectionSessionAsync(new[] { cleanIp }, _lastTeacherIp, _fileService.CurrentCollectionFolder, _fileService.CurrentSessionTitle, _fileService.PrefixMachineName, _fileService.SeparateMachineFolder);
                }
            }
            else
            {
                OnStudentUpdated?.Invoke(existing);
            }
        }
        else
        {
            var newNode = new StudentNode
            {
                Id = string.IsNullOrWhiteSpace(packet.MachineId) ? Guid.NewGuid().ToString("N") : packet.MachineId,
                MachineName = packet.MachineName,
                CurrentUser = packet.CurrentUser,
                IpAddress = cleanIp,
                MacAddress = packet.MacAddress,
                CommandPort = packet.CommandPort,
                Status = packet.Status,
                AppVersion = packet.AppVersion,
                LastSeen = DateTime.UtcNow
            };

            if (_students.TryAdd(key, newNode))
            {
                OnStudentJoined?.Invoke(newNode);
                if (_fileService.IsCollecting && !string.IsNullOrWhiteSpace(_lastTeacherIp))
                {
                    _ = _fileService.StartCollectionSessionAsync(new[] { cleanIp }, _lastTeacherIp, _fileService.CurrentCollectionFolder, _fileService.CurrentSessionTitle, _fileService.PrefixMachineName, _fileService.SeparateMachineFolder);
                }
            }
        }
    }

    public async Task<int> ScanMachinesAsync(string? targetIpOrRange = null, CancellationToken ct = default, Action<int, int>? onProgress = null)
    {
        // 1. Phát beacon ngay lập tức tới tất cả các card mạng (broadcast)
        await _discoveryService.BroadcastBeaconNowAsync();

        // 2. Xác định danh sách IP mục tiêu
        List<IPAddress> targetIps;
        if (!string.IsNullOrWhiteSpace(targetIpOrRange))
        {
            targetIps = ParseIpInput(targetIpOrRange);
        }
        else
        {
            targetIps = TeacherDiscoveryService.GetLocalSubnetHostIps();
        }

        // 3. Thực hiện quét unicast dải IP
        if (targetIps.Count > 0)
        {
            await _discoveryService.ScanIpsAsync(targetIps, ct, onProgress);
        }

        // Đợi một khoảng ngắn để máy học sinh phản hồi đầy đủ qua mạng LAN
        try
        {
            await Task.Delay(800, ct);
        }
        catch (OperationCanceledException) { }

        return _students.Values.Count(s => s.Status != MachineStatus.Offline);
    }

    public static List<IPAddress> ParseIpInput(string input)
    {
        var list = new List<IPAddress>();
        if (string.IsNullOrWhiteSpace(input)) return list;

        string trimmed = input.Trim();

        // 1. IP đơn lẻ: 192.168.1.100
        if (IPAddress.TryParse(trimmed, out var singleIp))
        {
            list.Add(singleIp);
            return list;
        }

        // 2. Dải IP có dấu gạch ngang: 192.168.1.1 - 192.168.1.50 hoặc 192.168.1.1-50
        if (trimmed.Contains('-'))
        {
            var parts = trimmed.Split('-');
            if (parts.Length == 2)
            {
                string startStr = parts[0].Trim();
                string endStr = parts[1].Trim();

                if (IPAddress.TryParse(startStr, out var startIp))
                {
                    byte[] startBytes = startIp.GetAddressBytes();
                    if (endStr.Contains('.') && IPAddress.TryParse(endStr, out var endIp))
                    {
                        byte[] endBytes = endIp.GetAddressBytes();
                        if (startBytes.Length == 4 && endBytes.Length == 4 &&
                            startBytes[0] == endBytes[0] && startBytes[1] == endBytes[1] && startBytes[2] == endBytes[2])
                        {
                            int from = startBytes[3];
                            int to = endBytes[3];
                            if (from > to) (from, to) = (to, from);
                            for (int i = from; i <= to; i++)
                            {
                                list.Add(new IPAddress(new byte[] { startBytes[0], startBytes[1], startBytes[2], (byte)i }));
                            }
                            return list;
                        }
                    }
                    else if (int.TryParse(endStr, out int endHost) && endHost >= 1 && endHost <= 254)
                    {
                        int from = startBytes[3];
                        int to = endHost;
                        if (from > to) (from, to) = (to, from);
                        for (int i = from; i <= to; i++)
                        {
                            list.Add(new IPAddress(new byte[] { startBytes[0], startBytes[1], startBytes[2], (byte)i }));
                        }
                        return list;
                    }
                }
            }
        }

        // 3. CIDR: 192.168.1.0/24
        if (trimmed.Contains('/'))
        {
            var parts = trimmed.Split('/');
            if (parts.Length == 2 && IPAddress.TryParse(parts[0].Trim(), out var baseIp) && int.TryParse(parts[1].Trim(), out int prefix))
            {
                if (prefix == 24)
                {
                    byte[] b = baseIp.GetAddressBytes();
                    for (int i = 1; i <= 254; i++)
                    {
                        list.Add(new IPAddress(new byte[] { b[0], b[1], b[2], (byte)i }));
                    }
                    return list;
                }
            }
        }

        return list;
    }

    public static async Task<bool> CheckTcpCommandPortAsync(string ip, int timeoutMs = 500)
    {
        try
        {
            using var tcp = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);
            await tcp.ConnectAsync(ip, NetworkConfig.DefaultCommandPort, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> SetDriveSecurityAsync(DriveSecurityAction action, string driveLetter = @"D:\", IEnumerable<string>? targetIps = null)
    {
        var packet = new DriveSecurityPacket
        {
            Action = action,
            DriveLetter = driveLetter
        };

        byte[] packetBytes = PacketSerializer.Serialize(CommandType.DriveSecurityControl, packet);
        var ips = targetIps?.ToList() ?? _students.Values
            .Where(s => s.Status != MachineStatus.Offline && !string.IsNullOrWhiteSpace(s.IpAddress))
            .Select(s => s.IpAddress)
            .ToList();

        if (ips.Count == 0) return 0;

        var results = await _commandSender.SendToMultipleAsync(ips, packetBytes);
        return results.Values.Count(success => success);
    }

    private void CheckOfflineStudents(object? state)
    {
        var now = DateTime.UtcNow;
        foreach (var student in _students.Values)
        {
            if (student.Status != MachineStatus.Offline && (now - student.LastSeen) > OfflineThreshold)
            {
                student.Status = MachineStatus.Offline;
                OnStudentOffline?.Invoke(student);
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _healthCheckTimer.Dispose();
        _broadcastSender?.Dispose();
        _fileService.Dispose();
        _updateServer.Dispose();
        _deployServer.Dispose();
        _eventListener.Dispose();
        _screenReceiver.Dispose();
        _discoveryService.Dispose();
        _udpQuickSender.Dispose();
    }
}
