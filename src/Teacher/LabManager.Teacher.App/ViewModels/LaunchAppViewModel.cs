using System.IO;
using System.Windows;
using Microsoft.Win32;
using LabManager.Teacher.Core.Services;

namespace LabManager.Teacher.App.ViewModels;

public class AppPresetItem
{
    public string Name { get; set; } = string.Empty;
    public string ExecutableName { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class LaunchAppViewModel : ViewModelBase
{
    private readonly ClassroomManager _manager;
    private readonly List<string> _targetIps;
    private readonly bool _isAll;
    private string _appNameOrPath = "winword.exe";
    private string _arguments = string.Empty;
    private string _targetDescription = string.Empty;

    public string AppNameOrPath
    {
        get => _appNameOrPath;
        set => SetProperty(ref _appNameOrPath, value);
    }

    public string Arguments
    {
        get => _arguments;
        set => SetProperty(ref _arguments, value);
    }

    public string TargetDescription
    {
        get => _targetDescription;
        set => SetProperty(ref _targetDescription, value);
    }

    public List<AppPresetItem> Presets { get; } =
    [
        new() { Name = "Word", ExecutableName = "winword.exe", Icon = "📄", Description = "Soạn thảo văn bản MS Word" },
        new() { Name = "Excel", ExecutableName = "excel.exe", Icon = "📊", Description = "Bảng tính MS Excel" },
        new() { Name = "PowerPoint", ExecutableName = "powerpnt.exe", Icon = "📑", Description = "Trình chiếu MS PowerPoint" },
        new() { Name = "VS Code", ExecutableName = "code.exe", Icon = "💻", Description = "Visual Studio Code" },
        new() { Name = "Scratch", ExecutableName = "scratch.exe", Icon = "🐱", Description = "Scratch Desktop" },
        new() { Name = "Python IDLE", ExecutableName = "idle.exe", Icon = "🐍", Description = "Môi trường lập trình Python IDLE" },
        new() { Name = "Dev-C++", ExecutableName = "devcpp.exe", Icon = "⚙️", Description = "Lập trình C / C++" },
        new() { Name = "Paint", ExecutableName = "mspaint.exe", Icon = "🎨", Description = "Vẽ đồ họa Paint" },
        new() { Name = "Máy tính", ExecutableName = "calc.exe", Icon = "🔢", Description = "Máy tính tính toán Calculator" },
        new() { Name = "Notepad", ExecutableName = "notepad.exe", Icon = "📝", Description = "Trình ghi chú Notepad" },
        new() { Name = "Command Prompt", ExecutableName = "cmd.exe", Icon = "⌨️", Description = "Cửa sổ lệnh CMD" }
    ];

    public RelayCommand SendCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand SelectPresetCommand { get; }
    public RelayCommand BrowseFileCommand { get; }

    public event Action? OnRequestClose;
    public event Action<string>? OnStatusUpdated;

    public LaunchAppViewModel(ClassroomManager manager, List<StudentCardViewModel> targets, bool isAll)
    {
        _manager = manager;
        _isAll = isAll;
        _targetIps = targets.Where(s => s.IsOnline && !string.IsNullOrWhiteSpace(s.IpAddress))
                            .Select(s => s.IpAddress)
                            .ToList();

        if (isAll)
        {
            TargetDescription = $"Toàn bộ phòng máy ({_targetIps.Count} máy trực tuyến)";
        }
        else if (targets.Count == 1)
        {
            var s = targets[0];
            TargetDescription = $"Máy {s.MachineName} ({s.IpAddress})";
        }
        else
        {
            TargetDescription = $"{targets.Count} máy học sinh đã chọn";
        }

        SelectPresetCommand = new RelayCommand(param =>
        {
            if (param is AppPresetItem preset)
            {
                AppNameOrPath = preset.ExecutableName;
            }
        });

        BrowseFileCommand = new RelayCommand(() =>
        {
            var dialog = new OpenFileDialog
            {
                Title = "Chọn tệp thực thi ứng dụng (.exe)",
                Filter = "Tệp thực thi (*.exe)|*.exe|Tất cả tệp (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                AppNameOrPath = Path.GetFileName(dialog.FileName);
            }
        });

        CancelCommand = new RelayCommand(() => OnRequestClose?.Invoke());

        SendCommand = new RelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(AppNameOrPath))
            {
                MessageBox.Show("Vui lòng chọn hoặc nhập tên ứng dụng cần mở.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string cleanApp = AppNameOrPath.Trim();

            if (_isAll)
            {
                await _manager.SendLaunchAppAllAsync(cleanApp, Arguments);
                OnStatusUpdated?.Invoke($"Đã phát lệnh mở ứng dụng '{cleanApp}' tới toàn bộ phòng máy.");
            }
            else
            {
                await _manager.SendLaunchAppToTargetsAsync(_targetIps, cleanApp, Arguments);
                OnStatusUpdated?.Invoke($"Đã gửi lệnh mở ứng dụng '{cleanApp}' tới {TargetDescription}.");
            }

            OnRequestClose?.Invoke();
        });
    }
}
