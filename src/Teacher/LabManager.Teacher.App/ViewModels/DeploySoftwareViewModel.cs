using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using LabManager.Protocol.Packets;
using LabManager.Teacher.Core.Services;

namespace LabManager.Teacher.App.ViewModels;

public class SilentPresetItem
{
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class DeployTargetItemViewModel : ViewModelBase
{
    private bool _isSelected = true;
    private string _status = "Chờ lệnh";
    private int _progress;
    private string _statusColor = "#94A3B8";
    private string _detail = string.Empty;

    public string MachineId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;

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

    public int Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public string StatusColor
    {
        get => _statusColor;
        set => SetProperty(ref _statusColor, value);
    }

    public string Detail
    {
        get => _detail;
        set => SetProperty(ref _detail, value);
    }
}

public class DeploySoftwareViewModel : ViewModelBase, IDisposable
{
    private readonly ClassroomManager _manager;
    private string _installerPath = string.Empty;
    private string _arguments = "/quiet /norestart";
    private bool _isDeploying;
    private string _statusSummary = "Vui lòng chọn tệp cài đặt (.msi hoặc .exe) và danh sách máy đích.";
    private bool _isDisposed;

    public string InstallerPath
    {
        get => _installerPath;
        set
        {
            if (SetProperty(ref _installerPath, value))
            {
                OnPropertyChanged(nameof(FileName));
                AutoDetectPreset();
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string FileName => string.IsNullOrEmpty(_installerPath) ? "Chưa chọn tệp" : Path.GetFileName(_installerPath);

    public string Arguments
    {
        get => _arguments;
        set => SetProperty(ref _arguments, value);
    }

    public bool IsDeploying
    {
        get => _isDeploying;
        set
        {
            if (SetProperty(ref _isDeploying, value))
            {
                OnPropertyChanged(nameof(IsNotDeploying));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsNotDeploying => !_isDeploying;

    public string StatusSummary
    {
        get => _statusSummary;
        set => SetProperty(ref _statusSummary, value);
    }

    public ObservableCollection<DeployTargetItemViewModel> Targets { get; } = new();

    public List<SilentPresetItem> Presets { get; } =
    [
        new() { Name = "MSI Installer (/quiet /norestart)", Arguments = "/quiet /norestart", Description = "Gói chuẩn Windows Installer (.msi)" },
        new() { Name = "Inno Setup (/VERYSILENT /SUPPRESSMSGBOXES /NORESTART)", Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART", Description = "VS Code, Git, Notepad++, đa số app Inno" },
        new() { Name = "NSIS Installer (/S)", Arguments = "/S", Description = "VLC, 7-Zip, Chrome (.exe)" },
        new() { Name = "InstallShield (/s /v\"/qn\")", Arguments = "/s /v\"/qn\"", Description = "Bộ cài InstallShield" }
    ];

    public RelayCommand BrowseFileCommand { get; }
    public RelayCommand SelectPresetCommand { get; }
    public RelayCommand StartDeployCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand DeselectAllCommand { get; }
    public RelayCommand CloseCommand { get; }

    public event Action? RequestClose;

    public DeploySoftwareViewModel(ClassroomManager manager, IEnumerable<StudentCardViewModel> students, IEnumerable<string>? initialSelectedIps = null)
    {
        _manager = manager;

        var selectedSet = initialSelectedIps?.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var s in students.Where(s => s.IsOnline))
        {
            bool isTarget = selectedSet == null || selectedSet.Count == 0 || selectedSet.Contains(s.IpAddress);
            Targets.Add(new DeployTargetItemViewModel
            {
                MachineId = s.Id,
                MachineName = s.MachineName,
                IpAddress = s.IpAddress,
                IsSelected = isTarget
            });
        }

        BrowseFileCommand = new RelayCommand(_ => BrowseFile(), _ => !IsDeploying);
        SelectPresetCommand = new RelayCommand(param =>
        {
            if (param is string args)
                Arguments = args;
            else if (param is SilentPresetItem preset)
                Arguments = preset.Arguments;
        });
        StartDeployCommand = new RelayCommand(async _ => await StartDeployAsync(), _ => !IsDeploying && File.Exists(InstallerPath) && Targets.Any(t => t.IsSelected));
        SelectAllCommand = new RelayCommand(_ => SetAllSelection(true));
        DeselectAllCommand = new RelayCommand(_ => SetAllSelection(false));
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());

        _manager.OnDeploySoftwareStatusReceived += HandleDeployStatusReceived;
    }

    private void BrowseFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn gói cài đặt phần mềm cho học sinh (.msi hoặc .exe)",
            Filter = "Gói cài đặt phần mềm (*.msi;*.exe)|*.msi;*.exe|Windows Installer (*.msi)|*.msi|Ứng dụng thực thi (*.exe)|*.exe|Tất cả tệp (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            InstallerPath = dialog.FileName;
        }
    }

    private void AutoDetectPreset()
    {
        if (string.IsNullOrWhiteSpace(_installerPath)) return;

        string ext = Path.GetExtension(_installerPath).ToLowerInvariant();
        if (ext == ".msi")
        {
            Arguments = "/quiet /norestart";
        }
        else if (ext == ".exe")
        {
            // Mặc định cho exe thường là Inno Setup phổ biến
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART";
        }
    }

    private void SetAllSelection(bool selected)
    {
        foreach (var t in Targets)
            t.IsSelected = selected;
    }

    private async Task StartDeployAsync()
    {
        if (!File.Exists(InstallerPath))
        {
            MessageBox.Show("Tệp cài đặt không tồn tại. Vui lòng kiểm tra lại.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var selectedTargets = Targets.Where(t => t.IsSelected).ToList();
        if (selectedTargets.Count == 0)
        {
            MessageBox.Show("Vui lòng chọn ít nhất một máy học sinh để cài đặt.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Bạn có chắc chắn muốn cài đặt phần mềm từ xa cho {selectedTargets.Count} máy học sinh?\n\n" +
            $"Tệp: {FileName}\n" +
            $"Tham số: {Arguments}",
            "Xác nhận cài đặt phần mềm",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        IsDeploying = true;
        StatusSummary = $"Đang bắt đầu phát hành gói cài đặt đến {selectedTargets.Count} máy học sinh...";

        foreach (var t in selectedTargets)
        {
            t.Status = "Đang gửi lệnh...";
            t.Progress = 5;
            t.StatusColor = "#38BDF8"; // Cyan
            t.Detail = "Máy giáo viên đang gửi yêu cầu...";
        }

        try
        {
            var ips = selectedTargets.Select(t => t.IpAddress);
            int sent = await _manager.StartSoftwareDeployAsync(InstallerPath, Arguments, ips);
            StatusSummary = $"Đã gửi lệnh cài đặt thành công đến {sent}/{selectedTargets.Count} máy. Đang chờ báo cáo tiến độ...";
        }
        catch (Exception ex)
        {
            StatusSummary = $"Lỗi khi gửi lệnh cài đặt: {ex.Message}";
            IsDeploying = false;
        }
    }

    private void HandleDeployStatusReceived(DeploySoftwareStatusPacket packet, string machineName)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var target = Targets.FirstOrDefault(t => t.MachineId.Equals(packet.MachineId, StringComparison.OrdinalIgnoreCase))
                         ?? Targets.FirstOrDefault(t => t.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase));

            if (target == null) return;

            target.Progress = packet.ProgressPercentage;
            target.Detail = packet.Message;

            switch (packet.Stage)
            {
                case "Downloading":
                    target.Status = "Đang tải về...";
                    target.StatusColor = "#38BDF8"; // Sky blue
                    break;
                case "Installing":
                    target.Status = "Đang cài đặt ngầm...";
                    target.StatusColor = "#F59E0B"; // Amber
                    break;
                case "Completed":
                    target.Status = "Hoàn tất";
                    target.StatusColor = "#10B981"; // Emerald green
                    break;
                case "Failed":
                    target.Status = "Thất bại";
                    target.StatusColor = "#EF4444"; // Red
                    break;
                default:
                    target.Status = packet.Stage;
                    break;
            }

            int completedCount = Targets.Count(t => t.IsSelected && t.Status == "Hoàn tất");
            int failedCount = Targets.Count(t => t.IsSelected && t.Status == "Thất bại");
            int totalSelected = Targets.Count(t => t.IsSelected);

            if (completedCount + failedCount >= totalSelected)
            {
                IsDeploying = false;
                StatusSummary = $"Quá trình cài đặt hoàn thành: {completedCount} thành công, {failedCount} thất bại.";
            }
        });
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _manager.OnDeploySoftwareStatusReceived -= HandleDeployStatusReceived;
    }
}
