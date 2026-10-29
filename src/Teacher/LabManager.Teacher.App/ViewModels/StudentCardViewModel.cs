using System.IO;
using System.Windows.Media.Imaging;
using LabManager.Core.Enums;
using LabManager.Core.Models;

namespace LabManager.Teacher.App.ViewModels;

public class StudentCardViewModel : ViewModelBase
{
    private readonly StudentNode _node;
    private MachineStatus _status;
    private string _currentUser;
    private string _ipAddress;
    private string _appVersion;
    private BitmapSource? _thumbnailImage;
    private bool _isSelected;
    private bool _hasSubmitted;
    private string _submittedFileName = string.Empty;
    private DateTime? _submittedAt;

    public string Id => _node.Id;
    public string MachineName => _node.MachineName;
    public string MacAddress => _node.MacAddress;

    public bool HasSubmitted
    {
        get => _hasSubmitted;
        set => SetProperty(ref _hasSubmitted, value);
    }

    public string SubmittedFileName
    {
        get => _submittedFileName;
        set => SetProperty(ref _submittedFileName, value);
    }

    public DateTime? SubmittedAt
    {
        get => _submittedAt;
        set => SetProperty(ref _submittedAt, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    public string IpAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value);
    }

    public string AppVersion
    {
        get => _appVersion;
        set => SetProperty(ref _appVersion, value);
    }

    public BitmapSource? ThumbnailImage
    {
        get => _thumbnailImage;
        set
        {
            if (SetProperty(ref _thumbnailImage, value))
            {
                OnPropertyChanged(nameof(HasThumbnail));
            }
        }
    }

    public bool HasThumbnail => ThumbnailImage != null;

    public MachineStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(IsOnline));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }
    }

    public bool IsOnline => Status != MachineStatus.Offline;

    public string StatusText => Status switch
    {
        MachineStatus.Online => "Trực tuyến",
        MachineStatus.Locked => "Đã khóa",
        MachineStatus.BlackScreen => "Màn hình đen",
        MachineStatus.Broadcasting => "Đang xem bài giảng",
        _ => "Ngoại tuyến"
    };

    public string StatusColor => Status switch
    {
        MachineStatus.Online => "#10B981",       // Xanh lá (Emerald)
        MachineStatus.Locked => "#F59E0B",       // Vàng cam (Amber)
        MachineStatus.BlackScreen => "#EF4444",  // Đỏ (Red)
        MachineStatus.Broadcasting => "#3B82F6", // Xanh dương (Blue)
        _ => "#9CA3AF"                          // Xám (Gray)
    };

    public StudentCardViewModel(StudentNode node)
    {
        _node = node;
        _status = node.Status;
        _currentUser = node.CurrentUser;
        _ipAddress = node.IpAddress;
        _appVersion = node.AppVersion;
    }

    public void UpdateFrom(StudentNode node)
    {
        CurrentUser = node.CurrentUser;
        IpAddress = node.IpAddress;
        Status = node.Status;
        AppVersion = node.AppVersion;
    }

    public void UpdateThumbnail(byte[] jpegBytes)
    {
        try
        {
            using var stream = new MemoryStream(jpegBytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze(); // Đóng băng để truyền an toàn giữa các luồng

            ThumbnailImage = bitmap;
        }
        catch
        {
            // Bỏ qua lỗi khung hình hỏng nếu mạng giật
        }
    }
}
