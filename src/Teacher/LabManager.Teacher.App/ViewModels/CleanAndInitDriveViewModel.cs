using System.Windows;
using LabManager.Teacher.Core.Services;

namespace LabManager.Teacher.App.ViewModels;

public class CleanAndInitDriveViewModel : ViewModelBase
{
    private readonly ClassroomManager _manager;
    private readonly List<string> _targetIps;
    private readonly bool _isAll;
    private string _driveLetter = @"D:\";
    private string _folderListText = "10A1\n10A2\n10A3\n11A1\n11A2\n12A1";
    private bool _isConfirmed;
    private string _targetDescription = string.Empty;

    public string DriveLetter
    {
        get => _driveLetter;
        set => SetProperty(ref _driveLetter, value);
    }

    public string FolderListText
    {
        get => _folderListText;
        set => SetProperty(ref _folderListText, value);
    }

    public bool IsConfirmed
    {
        get => _isConfirmed;
        set => SetProperty(ref _isConfirmed, value);
    }

    public string TargetDescription
    {
        get => _targetDescription;
        set => SetProperty(ref _targetDescription, value);
    }

    public RelayCommand SendCommand { get; }
    public RelayCommand CancelCommand { get; }

    public event Action? OnRequestClose;
    public event Action<string>? OnStatusUpdated;

    public CleanAndInitDriveViewModel(ClassroomManager manager, List<StudentCardViewModel> targets, bool isAll)
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

        CancelCommand = new RelayCommand(() => OnRequestClose?.Invoke());

        SendCommand = new RelayCommand(async () =>
        {
            if (!IsConfirmed)
            {
                MessageBox.Show("Vui lòng tích chọn xác nhận để đồng ý xóa dữ liệu bài làm cũ.", "Cảnh báo an toàn", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string cleanDrive = string.IsNullOrWhiteSpace(DriveLetter) ? @"D:\" : DriveLetter.Trim();
            if (cleanDrive.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Không thể thực hiện trên ổ đĩa hệ điều hành C: vì lý do an toàn.", "Lỗi bảo mật", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var confirmResult = MessageBox.Show(
                $"BẠN CÓ CHẮC CHẮN MUỐN TIẾP TỤC?\n\nToàn bộ dữ liệu bài tập trên ổ {cleanDrive} tại {TargetDescription} sẽ bị XÓA VĨNH VIỄN và tạo lại các thư mục mới.\n\nThao tác này KHÔNG THỂ HOÀN TÁC!",
                "XÁC NHẬN NGUY HIỂM",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes)
            {
                return;
            }

            // Phân tách danh sách thư mục (mỗi dòng 1 tên)
            var folderNames = (FolderListText ?? string.Empty)
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (_isAll)
            {
                await _manager.SendCleanAndInitDriveAllAsync(cleanDrive, folderNames);
                OnStatusUpdated?.Invoke($"Đã phát lệnh dọn dẹp & khởi tạo ổ {cleanDrive} ({folderNames.Count} thư mục) tới toàn bộ phòng máy.");
            }
            else
            {
                await _manager.SendCleanAndInitDriveToTargetsAsync(_targetIps, cleanDrive, folderNames);
                OnStatusUpdated?.Invoke($"Đã gửi lệnh dọn dẹp & khởi tạo ổ {cleanDrive} ({folderNames.Count} thư mục) tới {TargetDescription}.");
            }

            OnRequestClose?.Invoke();
        });
    }
}
