using System.Collections.ObjectModel;
using System.Net;
using System.Windows;
using LabManager.Native.System;
using LabManager.Network.Discovery;
using LabManager.Teacher.Core.Services;

namespace LabManager.Teacher.App.ViewModels;

public class ScanMachinesViewModel : ViewModelBase
{
    private readonly ClassroomManager _classroomManager;
    private readonly ObservableCollection<StudentCardViewModel> _allStudents;
    private string _targetIpOrRange = string.Empty;
    private bool _isScanning;
    private double _progressValue;
    private string _progressText = "Sẵn sàng quét";
    private string _statusMessage = "Nhấn 'Quét toàn mạng' hoặc nhập địa chỉ IP máy bị thiếu để dò tìm.";
    private CancellationTokenSource? _scanCts;

    public string TargetIpOrRange
    {
        get => _targetIpOrRange;
        set => SetProperty(ref _targetIpOrRange, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(CanScan));
                ScanAllCommand.RaiseCanExecuteChanged();
                ScanSpecificIpCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanScan => !IsScanning;

    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int FoundCount => _allStudents.Count(s => s.IsOnline);
    public int TotalCount => _allStudents.Count;

    public string LocalNetworkInfo { get; }

    public ObservableCollection<string> ScanLog { get; } = new();

    public RelayCommand ScanAllCommand { get; }
    public RelayCommand ScanSpecificIpCommand { get; }
    public RelayCommand StopScanCommand { get; }

    public ScanMachinesViewModel(ClassroomManager classroomManager, ObservableCollection<StudentCardViewModel> allStudents)
    {
        _classroomManager = classroomManager;
        _allStudents = allStudents;

        // Lấy thông tin card mạng nội bộ
        var (localIp, _) = NetworkInfoHelper.GetActiveNetworkInfo();
        var broadcastList = TeacherDiscoveryService.GetAllBroadcastAddresses();
        string bcastStr = string.Join(", ", broadcastList.Select(b => b.ToString()));
        LocalNetworkInfo = $"IP Giáo viên: {localIp} • Broadcast: {bcastStr}";

        ScanAllCommand = new RelayCommand(async () => await StartScanAsync(null), () => CanScan);
        ScanSpecificIpCommand = new RelayCommand(async () => await StartScanAsync(TargetIpOrRange), () => CanScan && !string.IsNullOrWhiteSpace(TargetIpOrRange));
        StopScanCommand = new RelayCommand(StopScan, () => IsScanning);

        // Đăng ký lắng nghe phát hiện máy
        _classroomManager.OnStudentJoined += HandleStudentFound;
        _classroomManager.OnStudentUpdated += HandleStudentFound;

        AddLog($"[Khởi động] Hệ thống phát hiện mạng sẵn sàng. {LocalNetworkInfo}");
        AddLog($"[Hiện tại] Đang kết nối: {FoundCount} máy trực tuyến / {TotalCount} máy tổng số.");
    }

    private void HandleStudentFound(LabManager.Core.Models.StudentNode node)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            AddLog($"🟢 [{time}] Tìm thấy máy: {node.MachineName} (IP: {node.IpAddress}) - Trạng thái: {node.Status}");
            OnPropertyChanged(nameof(FoundCount));
            OnPropertyChanged(nameof(TotalCount));
        });
    }

    public async Task StartScanAsync(string? targetInput)
    {
        if (IsScanning) return;

        IsScanning = true;
        _scanCts = new CancellationTokenSource();
        ProgressValue = 0;

        string targetDesc = string.IsNullOrWhiteSpace(targetInput)
            ? "toàn bộ dải IP mạng LAN"
            : $"mục tiêu '{targetInput}'";

        ProgressText = $"Đang bắt đầu quét {targetDesc}...";
        StatusMessage = $"🔍 Đang phát tín hiệu tìm kiếm tới {targetDesc}...";
        AddLog($"--- BẮT ĐẦU QUÉT: {targetDesc} ---");

        try
        {
            // Kiểm tra nếu là IP đơn lẻ: kiểm tra thêm cổng TCP 9001
            if (!string.IsNullOrWhiteSpace(targetInput) && IPAddress.TryParse(targetInput.Trim(), out var parsedIp))
            {
                AddLog($"[Thử nghiệm TCP] Đang kiểm tra cổng dịch vụ 9001 trên {parsedIp}...");
                bool portOpen = await ClassroomManager.CheckTcpCommandPortAsync(parsedIp.ToString(), 700);
                if (portOpen)
                {
                    AddLog($"✅ Cổng 9001 trên {parsedIp} ĐANG MỞ! LabManager Student đang chạy.");
                }
                else
                {
                    AddLog($"⚠️ Cổng 9001 trên {parsedIp} chưa phản hồi (có thể do tường lửa chặn hoặc máy chưa bật Student).");
                }
            }

            int found = await _classroomManager.ScanMachinesAsync(
                targetInput,
                _scanCts.Token,
                (sent, total) =>
                {
                    double pct = total > 0 ? (double)sent / total * 100.0 : 0;
                    Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        ProgressValue = Math.Round(pct, 1);
                        ProgressText = $"Đang quét: {sent}/{total} địa chỉ IP ({ProgressValue:F0}%)...";
                    });
                });

            ProgressValue = 100;
            ProgressText = "Quét hoàn tất!";
            StatusMessage = $"✅ Quét xong! Đã tìm thấy {FoundCount} máy trực tuyến trong phòng máy.";
            AddLog($"--- QUÉT HOÀN TẤT: Hiện có {FoundCount} máy trực tuyến ---");

            if (FoundCount < 6)
            {
                AddLog("💡 Gợi ý: Nếu vẫn thiếu máy, hãy kiểm tra xem máy đó đã mở LabManager Student chưa và dây mạng LAN đã cắm chắc chắn.");
            }
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Đã dừng quét.";
            StatusMessage = "Đã dừng thao tác quét máy.";
            AddLog("⏹️ Quá trình quét đã được dừng bởi người dùng.");
        }
        catch (Exception ex)
        {
            ProgressText = "Lỗi khi quét";
            StatusMessage = $"Lỗi: {ex.Message}";
            AddLog($"❌ Lỗi trong quá trình quét: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    public void StopScan()
    {
        _scanCts?.Cancel();
    }

    private void AddLog(string message)
    {
        ScanLog.Insert(0, message);
        if (ScanLog.Count > 100)
        {
            ScanLog.RemoveAt(ScanLog.Count - 1);
        }
    }
}
