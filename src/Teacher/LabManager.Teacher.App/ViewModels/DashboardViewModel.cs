using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using LabManager.Core.Enums;
using LabManager.Native.System;
using LabManager.Teacher.App.Views;
using LabManager.Teacher.Core.Services;

namespace LabManager.Teacher.App.ViewModels;

public class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly ClassroomManager _classroomManager;
    private readonly ConcurrentDictionary<string, ProcessListViewModel> _activeProcessViews = new();
    private readonly ConcurrentDictionary<string, TeacherChatViewModel> _activeChats = new();
    private string _teacherName = "Giáo viên";
    private string _roomName = "Phòng Máy 01";
    private string _statusMessage = "Hệ thống quản lý phòng máy sẵn sàng.";
    private bool _showOnlyOnline;
    private int _selectedSortIndex;
    private bool _isDisposed;

    public ObservableCollection<StudentCardViewModel> Students { get; } = new();
    public ICollectionView DisplayedStudents { get; }

    public string TeacherName
    {
        get => _teacherName;
        set => SetProperty(ref _teacherName, value);
    }

    public string RoomName
    {
        get => _roomName;
        set => SetProperty(ref _roomName, value);
    }

    public int OnlineCount => Students.Count(s => s.IsOnline);
    public int TotalCount => Students.Count;
    public int SelectedCount => Students.Count(s => s.IsSelected);
    public bool HasSelection => SelectedCount > 0;

    public bool ShowOnlyOnline
    {
        get => _showOnlyOnline;
        set
        {
            if (SetProperty(ref _showOnlyOnline, value))
            {
                DisplayedStudents.Refresh();
            }
        }
    }

    private bool _isCollectingFiles;
    public bool IsCollectingFiles
    {
        get => _isCollectingFiles;
        set
        {
            if (SetProperty(ref _isCollectingFiles, value))
            {
                OnPropertyChanged(nameof(CurrentCollectionFolder));
            }
        }
    }

    private int _collectedCount;
    public int CollectedCount
    {
        get => _collectedCount;
        set => SetProperty(ref _collectedCount, value);
    }

    public string CurrentCollectionFolder => _classroomManager.CurrentCollectionFolder;

    public int SelectedSortIndex
    {
        get => _selectedSortIndex;
        set
        {
            if (SetProperty(ref _selectedSortIndex, value))
            {
                ApplySort();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBroadcasting => _classroomManager.IsBroadcasting;
    public string BroadcastButtonText
    {
        get
        {
            if (_classroomManager.IsStudentBroadcasting)
                return $"🛑 Dừng chiếu ({_classroomManager.BroadcastingStudentName})";
            return IsBroadcasting ? "⏹ Dừng chiếu GV" : "📺 Chiếu màn hình GV";
        }
    }
    public string BroadcastButtonColor => IsBroadcasting ? "#EF4444" : "#3B82F6";

    private bool _isLaserPointerActive;
    public bool IsLaserPointerActive
    {
        get => _isLaserPointerActive;
        set
        {
            if (SetProperty(ref _isLaserPointerActive, value))
            {
                _classroomManager.LaserPointerEnabled = value;
                OnPropertyChanged(nameof(LaserButtonText));
                OnPropertyChanged(nameof(LaserButtonColor));
                StatusMessage = value ? "🎯 Đã bật chế độ Bút Chiếu Laser cho bài giảng." : "Đã tắt Bút Chiếu Laser (về chế độ con trỏ chuẩn).";
            }
        }
    }

    public string LaserButtonText => IsLaserPointerActive ? "🔴 Tắt Laser" : "🎯 Bút Laser";
    public string LaserButtonColor => IsLaserPointerActive ? "#DC2626" : "#475569";

    public RelayCommand LockAllCommand { get; }
    public RelayCommand UnlockAllCommand { get; }
    public RelayCommand ToggleLockCommand { get; }
    public RelayCommand ToggleBroadcastCommand { get; }
    public RelayCommand DistributeFileCommand { get; }
    public RelayCommand CollectFilesCommand { get; }
    public RelayCommand StopCollectFilesCommand { get; }
    public RelayCommand OpenCollectionFolderCommand { get; }
    public RelayCommand UpgradeStudentsCommand { get; }
    public RelayCommand OpenUpdateManagerCommand { get; }
    public RelayCommand WakeAllCommand { get; }
    public RelayCommand RestartAllCommand { get; }
    public RelayCommand ShutdownAllCommand { get; }
    public RelayCommand WakeStudentCommand { get; }
    public RelayCommand RestartStudentCommand { get; }
    public RelayCommand ShutdownStudentCommand { get; }
    public RelayCommand OpenBlacklistConfigCommand { get; }
    public RelayCommand ViewProcessesCommand { get; }
    public RelayCommand OpenWebFilterConfigCommand { get; }
    public RelayCommand OpenBroadcastChatCommand { get; }
    public RelayCommand OpenStudentChatCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand DeselectAllCommand { get; }
    public RelayCommand ToggleSelectStudentCommand { get; }
    public RelayCommand StartRemoteControlCommand { get; }
    public RelayCommand BroadcastStudentCommand { get; }
    public RelayCommand MoveStudentLeftCommand { get; }
    public RelayCommand MoveStudentRightCommand { get; }
    public RelayCommand ToggleLaserPointerCommand { get; }
    public RelayCommand OpenWebWindowCommand { get; }
    public RelayCommand OpenLaunchAppWindowCommand { get; }
    public RelayCommand OpenStudentWebCommand { get; }
    public RelayCommand OpenStudentAppCommand { get; }
    public RelayCommand SetWallpaperCommand { get; }
    public RelayCommand SetStudentWallpaperCommand { get; }
    public RelayCommand DeploySoftwareCommand { get; }
    public RelayCommand DeployStudentSoftwareCommand { get; }
    public RelayCommand LockDriveDCommand { get; }
    public RelayCommand UnlockDriveDCommand { get; }
    public RelayCommand LockStudentDriveDCommand { get; }
    public RelayCommand UnlockStudentDriveDCommand { get; }

    public DashboardViewModel()
    {
        _classroomManager = new ClassroomManager();

        DisplayedStudents = CollectionViewSource.GetDefaultView(Students);
        DisplayedStudents.Filter = item =>
        {
            if (item is not StudentCardViewModel s) return false;
            return !_showOnlyOnline || s.IsOnline;
        };

        ToggleLaserPointerCommand = new RelayCommand(() => IsLaserPointerActive = !IsLaserPointerActive);
        LockAllCommand = new RelayCommand(async () => await LockSelectedOrAllAsync(true));
        UnlockAllCommand = new RelayCommand(async () => await LockSelectedOrAllAsync(false));
        ToggleLockCommand = new RelayCommand(async (param) => await ToggleLockStudentAsync(param as StudentCardViewModel));
        ToggleBroadcastCommand = new RelayCommand(async () => await ToggleBroadcastAsync());
        BroadcastStudentCommand = new RelayCommand(async (param) => await BroadcastStudentAsync(param as StudentCardViewModel));
        DistributeFileCommand = new RelayCommand(async () => await DistributeFileAsync());
        CollectFilesCommand = new RelayCommand(async () => await CollectFilesAsync());
        StopCollectFilesCommand = new RelayCommand(async () => await StopCollectFilesAsync());
        OpenCollectionFolderCommand = new RelayCommand(OpenCollectionFolder);
        OpenUpdateManagerCommand = new RelayCommand(param => OpenUpdateManager(param as StudentCardViewModel));
        UpgradeStudentsCommand = new RelayCommand(param => OpenUpdateManager(param as StudentCardViewModel));
        WakeAllCommand = new RelayCommand(async () => await WakeAllAsync());
        RestartAllCommand = new RelayCommand(async () => await PowerSelectedOrAllAsync(PowerAction.Restart));
        ShutdownAllCommand = new RelayCommand(async () => await PowerSelectedOrAllAsync(PowerAction.Shutdown));
        WakeStudentCommand = new RelayCommand(async (param) => await WakeStudentAsync(param as StudentCardViewModel));
        RestartStudentCommand = new RelayCommand(async (param) => await PowerStudentAsync(param as StudentCardViewModel, PowerAction.Restart));
        ShutdownStudentCommand = new RelayCommand(async (param) => await PowerStudentAsync(param as StudentCardViewModel, PowerAction.Shutdown));
        OpenBlacklistConfigCommand = new RelayCommand(OpenBlacklistConfig);
        ViewProcessesCommand = new RelayCommand(async (param) => await ViewProcessesAsync(param as StudentCardViewModel));
        OpenWebFilterConfigCommand = new RelayCommand(OpenWebFilterConfig);
        OpenBroadcastChatCommand = new RelayCommand(OpenBroadcastChat);
        OpenStudentChatCommand = new RelayCommand(param => OpenStudentChat(param as StudentCardViewModel));

        SelectAllCommand = new RelayCommand(SelectAll);
        DeselectAllCommand = new RelayCommand(DeselectAll);
        ToggleSelectStudentCommand = new RelayCommand(param => ToggleSelectStudent(param as StudentCardViewModel));
        StartRemoteControlCommand = new RelayCommand(param => StartRemoteControl(param as StudentCardViewModel));
        MoveStudentLeftCommand = new RelayCommand(param => MoveStudent(param as StudentCardViewModel, -1));
        MoveStudentRightCommand = new RelayCommand(param => MoveStudent(param as StudentCardViewModel, 1));
        OpenWebWindowCommand = new RelayCommand(() => OpenWebDialog());
        OpenLaunchAppWindowCommand = new RelayCommand(() => OpenLaunchAppDialog());
        OpenStudentWebCommand = new RelayCommand(param => OpenWebDialog(param as StudentCardViewModel));
        OpenStudentAppCommand = new RelayCommand(param => OpenLaunchAppDialog(param as StudentCardViewModel));
        SetWallpaperCommand = new RelayCommand(async () => await SetWallpaperAsync());
        SetStudentWallpaperCommand = new RelayCommand(async param => await SetWallpaperAsync(param as StudentCardViewModel));
        DeploySoftwareCommand = new RelayCommand(() => OpenDeploySoftwareDialog());
        DeployStudentSoftwareCommand = new RelayCommand(param => OpenDeploySoftwareDialog(param as StudentCardViewModel));
        LockDriveDCommand = new RelayCommand(async () => await SetDriveSecuritySelectedOrAllAsync(DriveSecurityAction.LockDrive));
        UnlockDriveDCommand = new RelayCommand(async () => await SetDriveSecuritySelectedOrAllAsync(DriveSecurityAction.UnlockDrive));
        LockStudentDriveDCommand = new RelayCommand(async param => await SetDriveSecurityStudentAsync(param as StudentCardViewModel, DriveSecurityAction.LockDrive));
        UnlockStudentDriveDCommand = new RelayCommand(async param => await SetDriveSecurityStudentAsync(param as StudentCardViewModel, DriveSecurityAction.UnlockDrive));

        _classroomManager.OnFileCollected += (submission, savePath) =>
        {
            Dispatch(() =>
            {
                CollectedCount++;
                string displayFile = Path.GetFileName(savePath);
                StatusMessage = $"📥 Đã nhận bài ({CollectedCount}): {submission.MachineName} ({submission.StudentName}) - {displayFile}";

                var student = Students.FirstOrDefault(s =>
                    (!string.IsNullOrEmpty(submission.MachineId) && s.Id == submission.MachineId) ||
                    s.MachineName.Equals(submission.MachineName, StringComparison.OrdinalIgnoreCase));

                if (student != null)
                {
                    student.HasSubmitted = true;
                    student.SubmittedFileName = displayFile;
                    student.SubmittedAt = submission.SubmittedAt.ToLocalTime();
                }
            });
        };

        _classroomManager.OnChatMessageReceived += (msg, _) =>
        {
            Dispatch(() =>
            {
                StatusMessage = $"💬 [TIN NHẮN] {msg.SenderName}: {msg.Content}";
                if (_activeChats.TryGetValue(msg.SenderId, out var chatVm))
                {
                    chatVm.ReceiveMessage(msg);
                }
                else if (_activeChats.TryGetValue("ALL", out var allChatVm))
                {
                    allChatVm.ReceiveMessage(msg);
                }
            });
        };

        _classroomManager.OnProcessViolation += (violation, _) =>
        {
            Dispatch(() => StatusMessage = $"⚠️ [VI PHẠM ỨNG DỤNG] Máy {violation.MachineName} mở '{violation.ProcessName}' và đã bị tắt!");
        };

        _classroomManager.OnWebViolation += (violation, _) =>
        {
            Dispatch(() => StatusMessage = $"⚠️ [VI PHẠM WEB] Máy {violation.MachineName} đang mở '{violation.TabTitle}'");
        };

        _classroomManager.OnProcessListReceived += (response, _) =>
        {
            Dispatch(() =>
            {
                if (_activeProcessViews.TryGetValue(response.MachineId, out var vm))
                    vm.UpdateProcesses(response.Processes);
                else
                    _activeProcessViews.Values.FirstOrDefault(v => v.Title.Contains(response.MachineName))?.UpdateProcesses(response.Processes);
            });
        };

        _classroomManager.OnStudentJoined += OnStudentJoined;
        _classroomManager.OnStudentUpdated += OnStudentUpdated;
        _classroomManager.OnStudentOffline += OnStudentOffline;
        _classroomManager.OnScreenFrameReceived += OnScreenFrameReceived;

        _classroomManager.StartDiscovery(_teacherName, _roomName);
    }

    private void SelectAll()
    {
        foreach (var s in Students)
        {
            if (!_showOnlyOnline || s.IsOnline)
                s.IsSelected = true;
        }
        NotifySelection();
    }

    private void DeselectAll()
    {
        foreach (var s in Students)
            s.IsSelected = false;
        NotifySelection();
    }

    private void ToggleSelectStudent(StudentCardViewModel? student)
    {
        if (student == null) return;
        student.IsSelected = !student.IsSelected;
        NotifySelection();
    }

    private void NotifySelection()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }

    public void StartRemoteControl(StudentCardViewModel? student)
    {
        if (student == null || !student.IsOnline)
        {
            MessageBox.Show("Máy học sinh hiện đang ngoại tuyến, không thể kết nối điều khiển.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var vm = new RemoteControlViewModel(student.MachineName, student.IpAddress, _classroomManager, student.Id);
        var win = new RemoteControlWindow(vm)
        {
            Owner = Application.Current?.MainWindow
        };
        win.Show();
    }

    private void MoveStudent(StudentCardViewModel? student, int direction)
    {
        if (student == null) return;
        int curIdx = Students.IndexOf(student);
        if (curIdx < 0) return;
        int targetIdx = curIdx + direction;
        if (targetIdx >= 0 && targetIdx < Students.Count)
        {
            Students.Move(curIdx, targetIdx);
        }
    }

    private void ApplySort()
    {
        var list = Students.ToList();
        List<StudentCardViewModel> sorted = _selectedSortIndex switch
        {
            1 => list.OrderByDescending(s => s.MachineName).ToList(),
            2 => list.OrderBy(s => s.IpAddress).ToList(),
            3 => list.OrderByDescending(s => s.IsOnline).ThenBy(s => s.MachineName).ToList(),
            _ => list.OrderBy(s => s.MachineName).ToList()
        };

        for (int i = 0; i < sorted.Count; i++)
        {
            int old = Students.IndexOf(sorted[i]);
            if (old != i) Students.Move(old, i);
        }
    }

    private async Task LockSelectedOrAllAsync(bool lockState)
    {
        var targets = Students.Where(s => s.IsSelected && s.IsOnline).ToList();
        if (targets.Count > 0)
        {
            foreach (var s in targets)
            {
                await _classroomManager.SendLockCommandAsync(s.IpAddress, lockState, lockState);
                s.Status = lockState ? MachineStatus.Locked : MachineStatus.Online;
            }
            StatusMessage = $"Đã {(lockState ? "khóa" : "mở khóa")} {targets.Count} máy đã chọn.";
            return;
        }

        await _classroomManager.SendLockAllAsync(lockState, lockState);
        foreach (var s in Students.Where(s => s.IsOnline))
            s.Status = lockState ? MachineStatus.Locked : MachineStatus.Online;
        StatusMessage = $"Đã {(lockState ? "khóa" : "mở khóa")} toàn bộ phòng máy.";
    }

    private async Task ToggleLockStudentAsync(StudentCardViewModel? student)
    {
        if (student == null || !student.IsOnline) return;
        bool targetLock = student.Status != MachineStatus.Locked;
        bool success = await _classroomManager.SendLockCommandAsync(student.IpAddress, targetLock, targetLock);
        if (success)
            student.Status = targetLock ? MachineStatus.Locked : MachineStatus.Online;
    }

    private async Task SetDriveSecuritySelectedOrAllAsync(DriveSecurityAction action)
    {
        var targets = Students.Where(s => s.IsSelected && s.IsOnline).ToList();
        var targetIps = targets.Count > 0 ? targets.Select(s => s.IpAddress).ToList() : null;

        int count = await _classroomManager.SetDriveSecurityAsync(action, @"D:\", targetIps);
        string actionText = action == DriveSecurityAction.LockDrive
            ? "khóa cấu trúc ổ D (chỉ cho phép làm bài trong các thư mục có sẵn)"
            : "mở khóa ổ D";

        StatusMessage = targets.Count > 0
            ? $"Đã {actionText} trên {count}/{targets.Count} máy đã chọn."
            : $"Đã {actionText} trên {count} máy toàn phòng học.";
    }

    private async Task SetDriveSecurityStudentAsync(StudentCardViewModel? student, DriveSecurityAction action)
    {
        if (student == null || string.IsNullOrWhiteSpace(student.IpAddress)) return;

        int count = await _classroomManager.SetDriveSecurityAsync(action, @"D:\", new[] { student.IpAddress });
        string actionText = action == DriveSecurityAction.LockDrive ? "khóa ổ D" : "mở khóa ổ D";
        StatusMessage = count > 0
            ? $"Đã {actionText} thành công trên máy {student.MachineName} ({student.IpAddress})."
            : $"Không thể {actionText} trên máy {student.MachineName}.";
    }

    private async Task ToggleBroadcastAsync()
    {
        if (_classroomManager.IsBroadcasting)
        {
            await _classroomManager.StopBroadcastAsync();
            foreach (var s in Students.Where(s => s.Status == MachineStatus.Broadcasting))
                s.Status = MachineStatus.Online;
        }
        else
        {
            await _classroomManager.StartBroadcastAsync(fullscreen: true);
            foreach (var s in Students.Where(s => s.IsOnline && s.Status != MachineStatus.Locked))
                s.Status = MachineStatus.Broadcasting;
        }

        OnPropertyChanged(nameof(IsBroadcasting));
        OnPropertyChanged(nameof(BroadcastButtonText));
        OnPropertyChanged(nameof(BroadcastButtonColor));
    }

    private async Task BroadcastStudentAsync(StudentCardViewModel? student)
    {
        if (student == null || !student.IsOnline)
        {
            MessageBox.Show("Máy học sinh hiện đang ngoại tuyến, không thể phát màn hình.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_classroomManager.IsStudentBroadcasting && _classroomManager.BroadcastingStudentId == student.Id)
        {
            await _classroomManager.StopBroadcastAsync();
            foreach (var s in Students.Where(s => s.Status == MachineStatus.Broadcasting))
                s.Status = MachineStatus.Online;

            StatusMessage = $"Đã dừng chiếu màn hình học sinh {student.MachineName}.";
        }
        else
        {
            string presenterName = string.IsNullOrWhiteSpace(student.CurrentUser) ? student.MachineName : $"{student.CurrentUser} ({student.MachineName})";
            StatusMessage = $"Đang phát màn hình của {presenterName} cho cả lớp...";
            await _classroomManager.StartStudentBroadcastAsync(student.Id, student.IpAddress, presenterName, fullscreen: true);

            foreach (var s in Students.Where(s => s.IsOnline && s.Id != student.Id && s.Status != MachineStatus.Locked))
                s.Status = MachineStatus.Broadcasting;

            StatusMessage = $"📡 Đang chiếu bài làm của {presenterName} cho toàn bộ phòng máy.";
        }

        OnPropertyChanged(nameof(IsBroadcasting));
        OnPropertyChanged(nameof(BroadcastButtonText));
        OnPropertyChanged(nameof(BroadcastButtonColor));
    }

    private async Task DistributeFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn tệp bài tập để gửi đến học sinh",
            Filter = "Tất cả tệp (*.*)|*.*|Tệp nén (*.zip)|*.zip|Tài liệu (*.docx;*.pdf)|*.docx;*.pdf"
        };

        if (dialog.ShowDialog() == true)
        {
            var selectedIps = Students.Where(s => s.IsSelected && s.IsOnline).Select(s => s.IpAddress).ToList();
            StatusMessage = $"Đang gửi tệp: {Path.GetFileName(dialog.FileName)}...";
            int successCount = await _classroomManager.DistributeFileAsync(dialog.FileName, selectedIps.Count > 0 ? selectedIps : null);
            StatusMessage = $"Đã gửi tệp đến {successCount} máy học sinh.";
        }
    }

    private async Task CollectFilesAsync()
    {
        if (IsCollectingFiles)
        {
            var res = MessageBox.Show(
                $"Đang trong đợt thu bài!\nThư mục lưu: {_classroomManager.CurrentCollectionFolder}\nSố bài đã nộp: {CollectedCount}\n\nBạn có muốn KẾT THÚC đợt thu bài không?",
                "Quản lý thu bài",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                await StopCollectFilesAsync();
            }
            return;
        }

        var configDialog = new StartCollectionWindow();
        if (Application.Current.MainWindow != null)
        {
            configDialog.Owner = Application.Current.MainWindow;
        }

        if (configDialog.ShowDialog() != true) return;

        string selectedFolder = configDialog.SelectedFolder;
        string sessionTitle = configDialog.SessionTitle;
        bool prefixMachine = configDialog.PrefixMachineName;
        bool separateFolder = configDialog.SeparateMachineFolder;

        var (localIp, _) = NetworkInfoHelper.GetActiveNetworkInfo();
        if (string.IsNullOrWhiteSpace(localIp))
        {
            MessageBox.Show("Không tìm thấy địa chỉ IP mạng nội bộ của giáo viên để mở cổng thu bài.", "Lỗi mạng", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        CollectedCount = 0;
        foreach (var student in Students)
        {
            student.HasSubmitted = false;
            student.SubmittedFileName = string.Empty;
            student.SubmittedAt = null;
        }

        StatusMessage = "Đang phát lệnh mở đợt thu bài tới các máy học sinh...";
        await _classroomManager.StartCollectFilesSessionAsync(localIp, selectedFolder, sessionTitle, prefixMachine, separateFolder);
        IsCollectingFiles = true;

        string optDesc = prefixMachine && separateFolder
            ? "[Kèm tên máy & Tách thư mục]"
            : prefixMachine ? "[Kèm tên máy]" : separateFolder ? "[Tách thư mục riêng]" : "[Lưu tên gốc]";

        StatusMessage = $"📥 Đang mở đợt thu bài {optDesc}. Thư mục: {selectedFolder}";
    }

    private async Task StopCollectFilesAsync()
    {
        var (localIp, _) = NetworkInfoHelper.GetActiveNetworkInfo();
        await _classroomManager.StopCollectFilesSessionAsync(localIp);
        IsCollectingFiles = false;
        StatusMessage = $"⏹️ Đã kết thúc đợt thu bài. Tổng cộng nhận được: {CollectedCount} bài.";
    }

    private void OpenCollectionFolder()
    {
        string folder = _classroomManager.CurrentCollectionFolder;
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể mở thư mục: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task UpgradeStudentsAsync(StudentCardViewModel? specificStudent = null)
    {
        var onlineStudents = Students.Where(s => s.IsOnline).ToList();
        if (onlineStudents.Count == 0)
        {
            MessageBox.Show("Hiện không có máy học sinh nào trực tuyến để nâng cấp.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        List<StudentCardViewModel> targets;
        string targetDesc;

        if (specificStudent != null)
        {
            if (!specificStudent.IsOnline)
            {
                MessageBox.Show($"Máy học sinh {specificStudent.MachineName} hiện đang ngoại tuyến, không thể cập nhật.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            targets = [specificStudent];
            targetDesc = $"máy học sinh {specificStudent.MachineName} ({specificStudent.IpAddress})";
        }
        else
        {
            var selectedStudents = Students.Where(s => s.IsSelected && s.IsOnline).ToList();
            targets = selectedStudents.Count > 0 ? selectedStudents : onlineStudents;
            targetDesc = selectedStudents.Count > 0 
                ? $"{selectedStudents.Count} máy học sinh đã chọn" 
                : $"toàn bộ {onlineStudents.Count} máy học sinh trực tuyến";
        }

        // Luôn mở hộp thoại để giáo viên chủ động chọn tệp cài đặt mong muốn thay vì tự động chọn
        var dialog = new OpenFileDialog
        {
            Title = $"Chọn tệp cài đặt LabManager Student Setup (.exe) để nâng cấp {targetDesc}",
            Filter = "Bộ cài đặt Student Setup (*.exe)|*.exe|Tệp thực thi (*.exe)|*.exe|Tất cả tệp (*.*)|*.*",
            InitialDirectory = GetInitialSetupDirectory()
        };

        if (dialog.ShowDialog() != true)
            return;

        string setupPath = dialog.FileName;

        if (!File.Exists(setupPath))
        {
            MessageBox.Show("Tệp cài đặt được chọn không tồn tại hoặc không thể truy cập.", "Lỗi tệp cài đặt", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        string fileName = Path.GetFileName(setupPath);
        long fileSizeBytes = new FileInfo(setupPath).Length;
        double fileSizeMb = Math.Round((double)fileSizeBytes / (1024 * 1024), 2);
        string fileVersion = "Không xác định";
        try
        {
            var verInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(setupPath);
            if (!string.IsNullOrWhiteSpace(verInfo.FileVersion))
                fileVersion = verInfo.FileVersion;
            else if (!string.IsNullOrWhiteSpace(verInfo.ProductVersion))
                fileVersion = verInfo.ProductVersion;
        }
        catch { }

        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn phát hành nâng cấp cho {targetDesc}?\n\n" +
            $"• Tệp cài đặt: {fileName} ({fileSizeMb} MB)\n" +
            $"• Phiên bản bộ cài: v{fileVersion}\n" +
            $"• Vị trí tệp: {setupPath}\n\n" +
            "Hệ thống sẽ bắn gói cài đặt qua mạng LAN. Máy học sinh sẽ tự động tải về, cài đặt ngầm và khởi chạy phiên bản mới mà giáo viên không cần thao tác gì thêm.",
            "Xác nhận nâng cấp phòng máy",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        StatusMessage = $"Đang phát hành bản nâng cấp {fileName} (v{fileVersion}) tới {targets.Count} máy học sinh qua mạng LAN...";

        var targetIps = targets.Select(s => s.IpAddress).ToList();
        int notified = await _classroomManager.PushUpdateToStudentsAsync(setupPath, targetIps);

        StatusMessage = $"Đã phát lệnh tự động nâng cấp tới {notified}/{targets.Count} máy học sinh. Quá trình cài đặt ngầm đang diễn ra...";
    }

    public void OpenUpdateManager(StudentCardViewModel? target = null)
    {
        List<string>? initialSelected = null;
        if (target != null && !string.IsNullOrWhiteSpace(target.IpAddress))
        {
            initialSelected = [target.IpAddress];
        }
        else
        {
            var selectedCards = Students.Where(s => s.IsSelected && s.IsOnline).ToList();
            if (selectedCards.Count > 0)
            {
                initialSelected = selectedCards.Select(s => s.IpAddress).ToList();
            }
        }

        var vm = new UpdateManagerViewModel(_classroomManager, Students, initialSelected);
        var win = new UpdateManagerWindow(vm)
        {
            Owner = Application.Current?.MainWindow
        };
        win.ShowDialog();
    }

    private static string GetInitialSetupDirectory()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] candidateDirs =
        [
            Path.Combine(baseDir, "updates"),
            Path.Combine(baseDir, "dist"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "dist")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "dist")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "dist")),
            @"E:\antigravity\dist",
            baseDir
        ];

        foreach (var dir in candidateDirs)
        {
            if (Directory.Exists(dir))
                return dir;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    }

    private async Task WakeAllAsync()
    {
        StatusMessage = "Đang gửi tín hiệu Wake-on-LAN...";
        await _classroomManager.WakeOnLanAllAsync();
        StatusMessage = "Đã phát gói tin Wake-on-LAN tới toàn bộ phòng máy.";
    }

    private async Task PowerSelectedOrAllAsync(PowerAction action)
    {
        string actionName = action == PowerAction.Restart ? "khởi động lại" : "tắt";
        var targets = Students.Where(s => s.IsSelected && s.IsOnline).ToList();
        string targetDesc = targets.Count > 0 ? $"{targets.Count} máy đã chọn" : "TOÀN BỘ máy học sinh trong phòng";

        var result = MessageBox.Show($"Bạn có chắc chắn muốn {actionName} {targetDesc}?",
            "Xác nhận điều khiển nguồn", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        if (targets.Count > 0)
        {
            foreach (var s in targets)
                await _classroomManager.SendPowerCommandAsync(s.IpAddress, action, force: true, delaySeconds: 0);
            StatusMessage = $"Đã gửi lệnh {actionName} tới {targets.Count} máy đã chọn.";
        }
        else
        {
            await _classroomManager.SendPowerAllAsync(action, force: true, delaySeconds: 0);
            StatusMessage = $"Đã gửi lệnh {actionName} tới tất cả máy trực tuyến.";
        }
    }

    private async Task WakeStudentAsync(StudentCardViewModel? student)
    {
        if (student == null || string.IsNullOrWhiteSpace(student.MacAddress)) return;
        await _classroomManager.WakeOnLanAsync(student.MacAddress);
        StatusMessage = $"Đã gửi gói Wake-on-LAN tới {student.MachineName}.";
    }

    private async Task PowerStudentAsync(StudentCardViewModel? student, PowerAction action)
    {
        if (student == null || !student.IsOnline) return;
        string actionName = action == PowerAction.Restart ? "khởi động lại" : "tắt";
        bool success = await _classroomManager.SendPowerCommandAsync(student.IpAddress, action, force: true, delaySeconds: 0);
        StatusMessage = success ? $"Đã gửi lệnh {actionName} tới {student.MachineName}." : $"Không thể gửi lệnh tới {student.MachineName}.";
    }

    private void OpenBlacklistConfig()
    {
        var vm = new BlacklistConfigViewModel(_classroomManager);
        new BlacklistConfigWindow(vm) { Owner = Application.Current?.MainWindow }.ShowDialog();
    }

    private void OpenWebFilterConfig()
    {
        var vm = new WebFilterConfigViewModel(_classroomManager);
        new WebFilterConfigWindow(vm) { Owner = Application.Current?.MainWindow }.ShowDialog();
    }

    private void OpenWebDialog(StudentCardViewModel? specificStudent = null)
    {
        var onlineStudents = Students.Where(s => s.IsOnline).ToList();
        if (onlineStudents.Count == 0)
        {
            MessageBox.Show("Hiện không có máy học sinh nào trực tuyến để mở trang web.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        List<StudentCardViewModel> targets;
        bool isAll = false;

        if (specificStudent != null)
        {
            if (!specificStudent.IsOnline)
            {
                MessageBox.Show($"Máy {specificStudent.MachineName} hiện đang ngoại tuyến.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            targets = [specificStudent];
        }
        else
        {
            var selected = Students.Where(s => s.IsSelected && s.IsOnline).ToList();
            if (selected.Count > 0)
            {
                targets = selected;
            }
            else
            {
                targets = onlineStudents;
                isAll = true;
            }
        }

        var vm = new OpenWebViewModel(_classroomManager, targets, isAll);
        vm.OnStatusUpdated += msg => StatusMessage = msg;
        new OpenWebWindow(vm) { Owner = Application.Current?.MainWindow }.ShowDialog();
    }

    private void OpenLaunchAppDialog(StudentCardViewModel? specificStudent = null)
    {
        var onlineStudents = Students.Where(s => s.IsOnline).ToList();
        if (onlineStudents.Count == 0)
        {
            MessageBox.Show("Hiện không có máy học sinh nào trực tuyến để mở ứng dụng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        List<StudentCardViewModel> targets;
        bool isAll = false;

        if (specificStudent != null)
        {
            if (!specificStudent.IsOnline)
            {
                MessageBox.Show($"Máy {specificStudent.MachineName} hiện đang ngoại tuyến.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            targets = [specificStudent];
        }
        else
        {
            var selected = Students.Where(s => s.IsSelected && s.IsOnline).ToList();
            if (selected.Count > 0)
            {
                targets = selected;
            }
            else
            {
                targets = onlineStudents;
                isAll = true;
            }
        }

        var vm = new LaunchAppViewModel(_classroomManager, targets, isAll);
        vm.OnStatusUpdated += msg => StatusMessage = msg;
        new LaunchAppWindow(vm) { Owner = Application.Current?.MainWindow }.ShowDialog();
    }

    private async Task SetWallpaperAsync(StudentCardViewModel? specificStudent = null)
    {
        var onlineStudents = Students.Where(s => s.IsOnline).ToList();
        if (onlineStudents.Count == 0)
        {
            MessageBox.Show("Hiện không có máy học sinh nào trực tuyến để đổi hình nền.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Chọn tệp hình ảnh để đặt làm hình nền máy học sinh",
            Filter = "Tệp hình ảnh (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|Tất cả tệp (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        List<string> targetIps;
        string targetDesc;

        if (specificStudent != null)
        {
            if (!specificStudent.IsOnline)
            {
                MessageBox.Show($"Máy {specificStudent.MachineName} hiện đang ngoại tuyến.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            targetIps = [specificStudent.IpAddress];
            targetDesc = $"máy {specificStudent.MachineName}";
        }
        else
        {
            var selected = Students.Where(s => s.IsSelected && s.IsOnline).ToList();
            if (selected.Count > 0)
            {
                targetIps = selected.Select(s => s.IpAddress).ToList();
                targetDesc = $"{selected.Count} máy học sinh đã chọn";
            }
            else
            {
                targetIps = onlineStudents.Select(s => s.IpAddress).ToList();
                targetDesc = $"toàn bộ {onlineStudents.Count} máy học sinh trực tuyến";
            }
        }

        StatusMessage = $"Đang gửi hình nền đến {targetDesc}...";
        int successCount = await _classroomManager.SendWallpaperAsync(dialog.FileName, 10, targetIps);
        StatusMessage = $"Đã áp dụng hình nền thành công trên {successCount}/{targetIps.Count} máy học sinh.";
    }

    private void OpenDeploySoftwareDialog(StudentCardViewModel? specificStudent = null)
    {
        var onlineStudents = Students.Where(s => s.IsOnline).ToList();
        if (onlineStudents.Count == 0)
        {
            MessageBox.Show("Hiện không có máy học sinh nào trực tuyến để cài đặt phần mềm.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        List<string>? initialSelected = null;
        if (specificStudent != null)
        {
            initialSelected = [specificStudent.IpAddress];
        }
        else
        {
            var selected = Students.Where(s => s.IsSelected && s.IsOnline).Select(s => s.IpAddress).ToList();
            if (selected.Count > 0)
                initialSelected = selected;
        }

        var vm = new DeploySoftwareViewModel(_classroomManager, Students, initialSelected);
        var win = new DeploySoftwareWindow(vm)
        {
            Owner = Application.Current?.MainWindow
        };
        win.ShowDialog();
    }

    private void OpenBroadcastChat()
    {
        if (_activeChats.TryGetValue("ALL", out _)) return;
        var vm = new TeacherChatViewModel(_classroomManager, "", "ALL", "Toàn phòng", isBroadcast: true);
        _activeChats["ALL"] = vm;
        var win = new TeacherChatWindow(vm) { Owner = Application.Current?.MainWindow };
        win.Closed += (_, _) => _activeChats.TryRemove("ALL", out _);
        win.Show();
    }

    private void OpenStudentChat(StudentCardViewModel? student)
    {
        if (student == null || !student.IsOnline) return;
        if (_activeChats.TryGetValue(student.Id, out _)) return;
        var vm = new TeacherChatViewModel(_classroomManager, student.IpAddress, student.Id, student.MachineName, isBroadcast: false);
        _activeChats[student.Id] = vm;
        var win = new TeacherChatWindow(vm) { Owner = Application.Current?.MainWindow };
        win.Closed += (_, _) => _activeChats.TryRemove(student.Id, out _);
        win.Show();
    }

    private async Task ViewProcessesAsync(StudentCardViewModel? student)
    {
        if (student == null || !student.IsOnline) return;
        var vm = new ProcessListViewModel(student.MachineName, student.IpAddress, _classroomManager);
        _activeProcessViews[student.Id] = vm;
        var win = new ProcessListWindow(vm) { Owner = Application.Current?.MainWindow };
        win.Closed += (_, _) => _activeProcessViews.TryRemove(student.Id, out _);
        win.Show();
        await _classroomManager.RequestProcessListAsync(student.IpAddress);
    }

    public void OnStudentHoverChanged(StudentCardViewModel? student, bool isHovered)
    {
        if (student == null || !student.IsOnline || string.IsNullOrWhiteSpace(student.IpAddress)) return;
        _classroomManager.SendFocusStream(student.IpAddress, isHovered);
    }

    private void OnScreenFrameReceived(LabManager.Protocol.Packets.ScreenFramePacket packet, string senderIp)
    {
        try
        {
            var student = Students.FirstOrDefault(s => s.Id == packet.MachineId || s.IpAddress == senderIp);
            if (student == null) return;

            // Giải mã BitmapImage ngay trên luồng UDP nền để không làm nghẽn UI Thread
            using var stream = new MemoryStream(packet.ImageBytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                student.ThumbnailImage = bitmap;
            }, System.Windows.Threading.DispatcherPriority.Render);
        }
        catch { }
    }

    private void Dispatch(Action action)
    {
        if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            Application.Current.Dispatcher.Invoke(action);
        else
            action();
    }

    private void HookStudentEvents(StudentCardViewModel card)
    {
        card.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StudentCardViewModel.IsSelected))
                NotifySelection();
            else if (e.PropertyName is nameof(StudentCardViewModel.IsOnline) or nameof(StudentCardViewModel.Status))
            {
                NotifyCounts();
                if (_showOnlyOnline) Dispatch(() => DisplayedStudents.Refresh());
            }
        };
    }

    private void OnStudentJoined(LabManager.Core.Models.StudentNode node)
    {
        Dispatch(() =>
        {
            var existing = Students.FirstOrDefault(s => s.Id == node.Id || (!string.IsNullOrEmpty(node.MacAddress) && s.MacAddress == node.MacAddress));
            if (existing != null)
                existing.UpdateFrom(node);
            else
            {
                var card = new StudentCardViewModel(node);
                HookStudentEvents(card);
                Students.Add(card);
                ApplySort();
            }
            NotifyCounts();
        });
    }

    private void OnStudentUpdated(LabManager.Core.Models.StudentNode node)
    {
        Dispatch(() =>
        {
            Students.FirstOrDefault(s => s.Id == node.Id)?.UpdateFrom(node);
            NotifyCounts();
        });
    }

    private void OnStudentOffline(LabManager.Core.Models.StudentNode node)
    {
        Dispatch(() =>
        {
            var existing = Students.FirstOrDefault(s => s.Id == node.Id);
            if (existing != null)
                existing.Status = MachineStatus.Offline;
            NotifyCounts();
        });
    }

    private void NotifyCounts()
    {
        OnPropertyChanged(nameof(OnlineCount));
        OnPropertyChanged(nameof(TotalCount));
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _classroomManager.Dispose();
    }
}
