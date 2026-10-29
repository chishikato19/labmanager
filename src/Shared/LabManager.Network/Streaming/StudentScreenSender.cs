using System.Net;
using System.Net.Sockets;
using LabManager.Core.Interfaces;
using LabManager.Core.Models;
using LabManager.Protocol.Packets;

namespace LabManager.Network.Streaming;

public class StudentScreenSender : IDisposable
{
    private const int ChunkDataSize = 1400; // Phù hợp MTU 1500 bytes tiêu chuẩn
    private readonly IScreenCapturer _capturer;
    private readonly string _machineId;
    private readonly Guid _machineGuid;
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private uint _frameIdCounter;
    private volatile bool _isRemoteControlMode;
    private volatile bool _isFocusMode;
    private long _lastFocusPingTime;
    private string? _currentTargetIp;
    private bool _isDisposed;

    public bool IsStreaming => _cts is { IsCancellationRequested: false };

    public StudentScreenSender(IScreenCapturer capturer, string machineId)
    {
        _capturer = capturer;
        _machineId = machineId;
        _machineGuid = Guid.TryParse(machineId, out var g) ? g : Guid.NewGuid();
    }

    public void Start(string teacherIp, int streamPort = NetworkConfig.DefaultStreamPort, int targetFps = 2)
    {
        if (IsStreaming && _currentTargetIp == teacherIp) return;

        Stop();

        _cts = new CancellationTokenSource();
        _udpClient = new UdpClient();
        _currentTargetIp = teacherIp;

        if (!IPAddress.TryParse(teacherIp, out var ip))
            return;

        var targetEndpoint = new IPEndPoint(ip, streamPort);
        int baseIntervalMs = Math.Max(300, 1000 / Math.Max(1, targetFps));

        _ = StreamLoopAsync(targetEndpoint, baseIntervalMs, _cts.Token);
    }

    public void SetRemoteControlMode(bool active)
    {
        _isRemoteControlMode = active;
    }

    public void SetFocusMode(bool active)
    {
        _isFocusMode = active;
        if (active)
        {
            Interlocked.Exchange(ref _lastFocusPingTime, Environment.TickCount64);
        }
    }

    private async Task StreamLoopAsync(IPEndPoint targetEndpoint, int baseIntervalMs, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _udpClient != null)
        {
            try
            {
                // Tự động trở về chế độ tiết kiệm băng thông sau 5s nếu không nhận thêm tín hiệu hover
                if (_isFocusMode && (Environment.TickCount64 - Interlocked.Read(ref _lastFocusPingTime) > 5000))
                {
                    _isFocusMode = false;
                }

                bool isHighDef = _isRemoteControlMode || _isFocusMode;

                // Khi giáo viên chỉ chuột vào màn hình học sinh hoặc đang Điều khiển từ xa:
                // Độ phân giải GỐC / Native (targetWidth = null để ảnh cực nét, không resize, đọc rõ từng chữ/dòng code), chất lượng 80%, 20 FPS (50ms)
                // Khi ở chế độ giám sát ngầm: 640x360, chất lượng 60%, 2 FPS (500ms) để tối ưu băng thông phòng máy
                int? targetW = isHighDef ? null : 640;
                int? targetH = isHighDef ? null : 360;
                int quality = isHighDef ? 80 : 60;
                int sleepMs = isHighDef ? 50 : baseIntervalMs;

                byte[]? jpegBytes = _capturer.CaptureFrameJpeg(quality: quality, targetWidth: targetW, targetHeight: targetH);
                if (jpegBytes != null && jpegBytes.Length > 0)
                {
                    uint frameId = unchecked(++_frameIdCounter);
                    ushort chunkCount = (ushort)Math.Ceiling((double)jpegBytes.Length / ChunkDataSize);
                    ushort actualW = (ushort)(targetW ?? 1920);
                    ushort actualH = (ushort)(targetH ?? 1080);

                    for (ushort i = 0; i < chunkCount; i++)
                    {
                        int offset = i * ChunkDataSize;
                        int length = Math.Min(ChunkDataSize, jpegBytes.Length - offset);

                        byte[] packetBytes = new byte[ScreenChunkHeader.HeaderSize + length];
                        var header = new ScreenChunkHeader(_machineGuid, frameId, i, chunkCount, actualW, actualH);
                        header.WriteTo(packetBytes.AsSpan(0, ScreenChunkHeader.HeaderSize));

                        Buffer.BlockCopy(jpegBytes, offset, packetBytes, ScreenChunkHeader.HeaderSize, length);
                        await _udpClient.SendAsync(packetBytes, packetBytes.Length, targetEndpoint);
                    }
                }

                await Task.Delay(sleepMs, ct);
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Bỏ qua lỗi gửi tạm thời nếu mạng chập chờn
            }
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _udpClient?.Dispose();
        _udpClient = null;
        _currentTargetIp = null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
        _cts?.Dispose();
    }
}
