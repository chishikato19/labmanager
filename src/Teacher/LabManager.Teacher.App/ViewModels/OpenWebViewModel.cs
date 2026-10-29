using System.Windows;
using LabManager.Teacher.Core.Services;

namespace LabManager.Teacher.App.ViewModels;

public class WebPresetItem
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

public class OpenWebViewModel : ViewModelBase
{
    private readonly ClassroomManager _manager;
    private readonly List<string> _targetIps;
    private readonly bool _isAll;
    private string _url = "https://kahoot.it";
    private bool _forceChrome = true;
    private string _targetDescription = string.Empty;

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    public bool ForceChrome
    {
        get => _forceChrome;
        set => SetProperty(ref _forceChrome, value);
    }

    public string TargetDescription
    {
        get => _targetDescription;
        set => SetProperty(ref _targetDescription, value);
    }

    public List<WebPresetItem> Presets { get; } =
    [
        new() { Name = "Kahoot!", Url = "https://kahoot.it", Icon = "🎯" },
        new() { Name = "Quizizz", Url = "https://quizizz.com/join", Icon = "⚡" },
        new() { Name = "Google", Url = "https://www.google.com", Icon = "🔍" },
        new() { Name = "YouTube", Url = "https://www.youtube.com", Icon = "▶️" },
        new() { Name = "Canva", Url = "https://www.canva.com", Icon = "🎨" },
        new() { Name = "Scratch", Url = "https://scratch.mit.edu", Icon = "🐱" },
        new() { Name = "GeoGebra", Url = "https://www.geogebra.org/calculator", Icon = "📐" },
        new() { Name = "Code.org", Url = "https://code.org", Icon = "💻" },
        new() { Name = "Wikipedia", Url = "https://vi.wikipedia.org", Icon = "📖" },
        new() { Name = "VioEdu", Url = "https://vio.edu.vn", Icon = "🇻🇳" }
    ];

    public RelayCommand SendCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand SelectPresetCommand { get; }

    public event Action? OnRequestClose;
    public event Action<string>? OnStatusUpdated;

    public OpenWebViewModel(ClassroomManager manager, List<StudentCardViewModel> targets, bool isAll)
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
            if (param is WebPresetItem preset)
            {
                Url = preset.Url;
            }
        });

        CancelCommand = new RelayCommand(() => OnRequestClose?.Invoke());

        SendCommand = new RelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                MessageBox.Show("Vui lòng nhập đường link trang web cần mở.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string cleanUrl = Url.Trim();
            if (!cleanUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !cleanUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !cleanUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                cleanUrl = "https://" + cleanUrl;
            }

            if (_isAll)
            {
                await _manager.SendOpenUrlAllAsync(cleanUrl, ForceChrome);
                OnStatusUpdated?.Invoke($"Đã phát lệnh mở web {cleanUrl} tới toàn bộ phòng máy.");
            }
            else
            {
                await _manager.SendOpenUrlToTargetsAsync(_targetIps, cleanUrl, ForceChrome);
                OnStatusUpdated?.Invoke($"Đã gửi lệnh mở web {cleanUrl} tới {TargetDescription}.");
            }

            OnRequestClose?.Invoke();
        });
    }
}
