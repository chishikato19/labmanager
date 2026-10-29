using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;
using LabManager.Teacher.Core.Services;

namespace LabManager.Teacher.App.ViewModels;

public class UpdateStudentTargetItemViewModel : ViewModelBase
{
    private bool _isSelected = true;
    private string _status = "Sẵn sàng";
    private string _statusColor = "#64748B";

    public string MachineId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string AppVersion { get; set; } = "1.3.4";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string StatusColor
    {
        get => _statusColor;
        set => SetProperty(ref _statusColor, value);
    }
}

public class UpdateManagerViewModel : ViewModelBase
{
    private readonly ClassroomManager _classroomManager;
    private GitHubRelease? _latestRelease;
    private GitHubReleaseAsset? _teacherAsset;
    private GitHubReleaseAsset? _studentAsset;

    private bool _isChecking;
    private string _checkStatus = "Đang kiểm tra bản cập nhật từ GitHub...";
    private string _latestVersion = "Đang tải...";
    private string _releaseTitle = string.Empty;
    private string _releaseNotes = string.Empty;
    private string _publishedDate = string.Empty;

    private bool _hasTeacherUpdate;
    private bool _isTeacherUpdating;
    private double _teacherProgress;
    private string _teacherStatusMessage = string.Empty;

    private bool _isStudentUpdating;
    private double _studentProgress;
    private string _studentStatusMessage = string.Empty;

    private bool _isSourceGit = true;
    private bool _isSourceLocal;
    private string _localFilePath = string.Empty;

    public string CurrentTeacherVersion { get; }
    public string RepositoryUrl => "https://github.com/chishikato19/labmanager";

    public GitHubRelease? LatestRelease
    {
        get => _latestRelease;
        private set => SetProperty(ref _latestRelease, value);
    }

    public bool IsChecking
    {
        get => _isChecking;
        set
        {
            if (SetProperty(ref _isChecking, value))
            {
                OnPropertyChanged(nameof(IsNotChecking));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsNotChecking => !_isChecking;

    public string CheckStatus
    {
        get => _checkStatus;
        set => SetProperty(ref _checkStatus, value);
    }

    public string LatestVersion
    {
        get => _latestVersion;
        set => SetProperty(ref _latestVersion, value);
    }

    public string ReleaseTitle
    {
        get => _releaseTitle;
        set => SetProperty(ref _releaseTitle, value);
    }

    public string ReleaseNotes
    {
        get => _releaseNotes;
        set => SetProperty(ref _releaseNotes, value);
    }

    public string PublishedDate
    {
        get => _publishedDate;
        set => SetProperty(ref _publishedDate, value);
    }

    public bool HasTeacherUpdate
    {
        get => _hasTeacherUpdate;
        set => SetProperty(ref _hasTeacherUpdate, value);
    }

    public bool IsTeacherUpdating
    {
        get => _isTeacherUpdating;
        set
        {
            if (SetProperty(ref _isTeacherUpdating, value))
            {
                OnPropertyChanged(nameof(CanUpdateTeacher));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool CanUpdateTeacher => !_isTeacherUpdating && _hasTeacherUpdate && _teacherAsset != null;

    public double TeacherProgress
    {
        get => _teacherProgress;
        set => SetProperty(ref _teacherProgress, value);
    }

    public string TeacherStatusMessage
    {
        get => _teacherStatusMessage;
        set => SetProperty(ref _teacherStatusMessage, value);
    }

    public bool IsStudentUpdating
    {
        get => _isStudentUpdating;
        set
        {
            if (SetProperty(ref _isStudentUpdating, value))
            {
                OnPropertyChanged(nameof(CanUpdateStudents));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool CanUpdateStudents => !_isStudentUpdating && Targets.Any(t => t.IsSelected);

    public double StudentProgress
    {
        get => _studentProgress;
        set => SetProperty(ref _studentProgress, value);
    }

    public string StudentStatusMessage
    {
        get => _studentStatusMessage;
        set => SetProperty(ref _studentStatusMessage, value);
    }

    public bool IsSourceGit
    {
        get => _isSourceGit;
        set
        {
            if (SetProperty(ref _isSourceGit, value))
            {
                if (value) _isSourceLocal = false;
                OnPropertyChanged(nameof(IsSourceLocal));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsSourceLocal
    {
        get => _isSourceLocal;
        set
        {
            if (SetProperty(ref _isSourceLocal, value))
            {
                if (value) _isSourceGit = false;
                OnPropertyChanged(nameof(IsSourceGit));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string LocalFilePath
    {
        get => _localFilePath;
        set
        {
            if (SetProperty(ref _localFilePath, value))
            {
                OnPropertyChanged(nameof(LocalFileName));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string LocalFileName => string.IsNullOrEmpty(_localFilePath) ? "Chưa chọn tệp" : Path.GetFileName(_localFilePath);

    public ObservableCollection<UpdateStudentTargetItemViewModel> Targets { get; } = new();

    public int SelectedCount => Targets.Count(t => t.IsSelected);
    public int TotalOnlineCount => Targets.Count;

    public RelayCommand CheckUpdatesCommand { get; }
    public RelayCommand UpdateTeacherCommand { get; }
    public RelayCommand UpdateStudentsCommand { get; }
    public RelayCommand BrowseLocalFileCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand DeselectAllCommand { get; }
    public RelayCommand CloseCommand { get; }

    public event Action? RequestClose;

    public UpdateManagerViewModel(ClassroomManager classroomManager, IEnumerable<StudentCardViewModel> students, IEnumerable<string>? initialSelectedIps = null)
    {
        _classroomManager = classroomManager;

        // Xác định version hiện tại của ứng dụng Giáo viên
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        CurrentTeacherVersion = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v1.3.4";

        var selectedIpsSet = initialSelectedIps?.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var s in students.Where(s => s.IsOnline))
        {
            bool isSelected = selectedIpsSet == null || selectedIpsSet.Count == 0 || selectedIpsSet.Contains(s.IpAddress);
            var item = new UpdateStudentTargetItemViewModel
            {
                MachineId = s.Id,
                MachineName = s.MachineName,
                IpAddress = s.IpAddress,
                AppVersion = s.AppVersion,
                IsSelected = isSelected
            };
            item.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(UpdateStudentTargetItemViewModel.IsSelected))
                {
                    OnPropertyChanged(nameof(SelectedCount));
                    OnPropertyChanged(nameof(CanUpdateStudents));
                }
            };
            Targets.Add(item);
        }

        CheckUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync(), () => !IsChecking && !IsTeacherUpdating && !IsStudentUpdating);
        UpdateTeacherCommand = new RelayCommand(async () => await UpdateTeacherAsync(), () => CanUpdateTeacher);
        UpdateStudentsCommand = new RelayCommand(async () => await UpdateStudentsAsync(), () => CanUpdateStudents);
        BrowseLocalFileCommand = new RelayCommand(BrowseLocalFile, () => !IsStudentUpdating);
        SelectAllCommand = new RelayCommand(() => SetAllSelection(true));
        DeselectAllCommand = new RelayCommand(() => SetAllSelection(false));
        CloseCommand = new RelayCommand(() => RequestClose?.Invoke(), () => !IsTeacherUpdating && !IsStudentUpdating);

        // Tự động kiểm tra GitHub ngay khi mở
        _ = CheckForUpdatesAsync();
    }

    public async Task CheckForUpdatesAsync()
    {
        IsChecking = true;
        CheckStatus = "Đang kết nối GitHub (chishikato19/labmanager)...";

        try
        {
            var release = await GitHubReleaseService.GetLatestReleaseAsync("chishikato19", "labmanager");
            LatestRelease = release;

            if (release == null)
            {
                LatestVersion = "Không thể kết nối";
                CheckStatus = "Không tìm thấy bản phát hành hoặc không có kết nối Internet.";
                return;
            }

            LatestVersion = release.TagName;
            ReleaseTitle = string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name;
            ReleaseNotes = string.IsNullOrWhiteSpace(release.Body) ? "Không có mô tả cập nhật." : release.Body;
            PublishedDate = release.PublishedAt.HasValue
                ? release.PublishedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                : "Gần đây";

            _teacherAsset = GitHubReleaseService.FindTeacherAsset(release);
            _studentAsset = GitHubReleaseService.FindStudentAsset(release);

            HasTeacherUpdate = GitHubReleaseService.IsNewerVersion(release.TagName, CurrentTeacherVersion);

            if (HasTeacherUpdate)
            {
                CheckStatus = $"Đã phát hiện bản mới {release.TagName} (Hiện tại: {CurrentTeacherVersion})";
            }
            else
            {
                CheckStatus = $"Phiên bản giáo viên đang dùng ({CurrentTeacherVersion}) là mới nhất.";
            }

            if (_studentAsset != null)
            {
                IsSourceGit = true;
            }
            else
            {
                IsSourceLocal = true;
            }
        }
        catch (Exception ex)
        {
            CheckStatus = $"Lỗi kiểm tra cập nhật: {ex.Message}";
        }
        finally
        {
            IsChecking = false;
            OnPropertyChanged(nameof(CanUpdateTeacher));
            OnPropertyChanged(nameof(CanUpdateStudents));
        }
    }

    public async Task UpdateTeacherAsync()
    {
        if (_teacherAsset == null)
        {
            MessageBox.Show("Không tìm thấy tệp cài đặt Teacher trong bản phát hành GitHub.", "Lỗi cập nhật", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn nâng cấp ứng dụng Giáo viên lên phiên bản {LatestVersion}?\n\n" +
            $"• Tệp cài đặt: {_teacherAsset.Name}\n" +
            $"• Dung lượng: {Math.Round((double)_teacherAsset.Size / (1024 * 1024), 2)} MB\n\n" +
            "Ứng dụng sẽ tự động tải bộ cài đặt, chạy cập nhật và khởi động lại phiên bản mới.",
            "Xác nhận cập nhật Giáo viên",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        IsTeacherUpdating = true;
        TeacherProgress = 0;
        TeacherStatusMessage = $"Đang tải {_teacherAsset.Name} từ GitHub...";

        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "LabManagerUpdates");
            string targetPath = Path.Combine(tempDir, _teacherAsset.Name);

            var progress = new Progress<double>(p =>
            {
                TeacherProgress = p;
                TeacherStatusMessage = $"Đang tải bộ cài đặt Giáo viên: {p:F1}%";
            });

            await GitHubReleaseService.DownloadAssetAsync(_teacherAsset, targetPath, progress);

            TeacherStatusMessage = "Tải thành công. Đang khởi chạy bộ cài đặt và khởi động lại...";
            await Task.Delay(800);

            // Chạy bộ cài Inno Setup với cờ im lặng và tự restart
            var psi = new ProcessStartInfo
            {
                FileName = targetPath,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true
            };

            Process.Start(psi);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            IsTeacherUpdating = false;
            TeacherStatusMessage = $"Lỗi cập nhật Giáo viên: {ex.Message}";
            MessageBox.Show($"Không thể hoàn tất nâng cấp: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task UpdateStudentsAsync()
    {
        var targets = Targets.Where(t => t.IsSelected).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show("Vui lòng chọn ít nhất một máy học sinh để nâng cấp.", "Chưa chọn máy", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string setupFilePath;

        if (IsSourceGit)
        {
            if (_studentAsset == null)
            {
                MessageBox.Show("Bản phát hành trên GitHub không chứa bộ cài Student (.exe). Vui lòng chọn tệp thủ công.", "Thiếu tệp cài đặt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string updatesCacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LabManager", "Updates");

            if (!Directory.Exists(updatesCacheDir))
                Directory.CreateDirectory(updatesCacheDir);

            setupFilePath = Path.Combine(updatesCacheDir, _studentAsset.Name);

            // Nếu file đã có sẵn trong cache và dung lượng khớp -> không cần tải lại
            bool needDownload = true;
            if (File.Exists(setupFilePath))
            {
                var fi = new FileInfo(setupFilePath);
                if (fi.Length == _studentAsset.Size)
                {
                    needDownload = false;
                }
            }

            if (needDownload)
            {
                IsStudentUpdating = true;
                StudentProgress = 0;
                StudentStatusMessage = $"Đang tải {_studentAsset.Name} từ GitHub về máy Giáo viên...";

                try
                {
                    var progress = new Progress<double>(p =>
                    {
                        StudentProgress = p;
                        StudentStatusMessage = $"Đang tải bộ cài học sinh từ GitHub: {p:F1}%";
                    });

                    await GitHubReleaseService.DownloadAssetAsync(_studentAsset, setupFilePath, progress);
                }
                catch (Exception ex)
                {
                    IsStudentUpdating = false;
                    StudentStatusMessage = $"Lỗi tải tệp từ GitHub: {ex.Message}";
                    MessageBox.Show($"Lỗi tải bộ cài học sinh từ GitHub: {ex.Message}", "Lỗi tải tệp", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(LocalFilePath) || !File.Exists(LocalFilePath))
            {
                MessageBox.Show("Tệp cài đặt được chọn không tồn tại hoặc không hợp lệ.", "Lỗi tệp cài đặt", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            setupFilePath = LocalFilePath;
        }

        // Bước 2: Phát hành qua mạng LAN nội bộ
        IsStudentUpdating = true;
        StudentProgress = 0;
        StudentStatusMessage = $"Đang kích hoạt máy chủ LAN và phát lệnh nâng cấp tới {targets.Count} máy học sinh...";

        foreach (var t in targets)
        {
            t.Status = "Đang gửi lệnh...";
            t.StatusColor = "#0284C7";
        }

        try
        {
            var targetIps = targets.Select(t => t.IpAddress).ToList();
            int notified = await _classroomManager.PushUpdateToStudentsAsync(setupFilePath, targetIps);

            foreach (var t in targets)
            {
                t.Status = "Đã nhận lệnh cập nhật (đang cài ngầm)";
                t.StatusColor = "#16A34A";
            }

            StudentProgress = 100;
            StudentStatusMessage = $"Đã phát lệnh thành công tới {notified}/{targets.Count} máy học sinh. Máy học sinh đang tự động tải và cài ngầm qua mạng LAN.";

            MessageBox.Show(
                $"Đã phát lệnh tự động nâng cấp tới {notified}/{targets.Count} máy học sinh!\n\n" +
                "Các máy học sinh sẽ tự tải file qua LAN, cài đặt ngầm (/VERYSILENT) và tự khởi động lại phiên bản mới.",
                "Nâng cấp phòng máy thành công",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StudentStatusMessage = $"Lỗi phát hành qua LAN: {ex.Message}";
            MessageBox.Show($"Lỗi phát hành qua LAN: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsStudentUpdating = false;
            OnPropertyChanged(nameof(CanUpdateStudents));
        }
    }

    private void BrowseLocalFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn tệp cài đặt LabManager Student Setup (.exe)",
            Filter = "Bộ cài đặt Student (*.exe)|*.exe|Tất cả tệp (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            LocalFilePath = dialog.FileName;
            IsSourceLocal = true;
        }
    }

    private void SetAllSelection(bool isSelected)
    {
        foreach (var t in Targets)
        {
            t.IsSelected = isSelected;
        }
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(CanUpdateStudents));
    }
}
