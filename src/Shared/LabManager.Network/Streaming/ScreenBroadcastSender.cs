using System.Net;
using System.Net.Sockets;
using LabManager.Core.Interfaces;
using LabManager.Core.Models;
using LabManager.Protocol.Packets;

namespace LabManager.Network.Streaming;

public class ScreenBroadcastSender : IDisposable
{
    private const int ChunkDataSize = 1400; // Tối ưu chuẩn MTU không phân mảnh gói tin UDP
    private readonly IScreenCapturer _capturer;
    private UdpClient? _udpClient;
    private IPEndPoint? _endpoint;
    private CancellationTokenSource? _cts;
    private uint _frameIdCounter;
    private bool _isDisposed;

    public bool IsBroadcasting => _cts is { IsCancellationRequested: false };

    public ScreenBroadcastSender(IScreenCapturer capturer)
    {
        _capturer = capturer;
    }

    public bool LaserPointerEnabled
    {
        get => _capturer.LaserPointerEnabled;
        set => _capturer.LaserPointerEnabled = value;
    }

    public void Start(string multicastGroup = NetworkConfig.DefaultMulticastGroup, 
                      int multicastPort = NetworkConfig.DefaultMulticastPort, 
                      int targetFps = 15)
    {
        if (IsBroadcasting) return;

        _cts = new CancellationTokenSource();
        _udpClient = new UdpClient();
        _udpClient.JoinMulticastGroup(IPAddress.Parse(multicastGroup));

        _endpoint = new IPEndPoint(IPAddress.Parse(multicastGroup), multicastPort);
        int intervalMs = Math.Max(50, 1000 / Math.Max(1, targetFps));

        _ = BroadcastLoopAsync(_endpoint, intervalMs, _cts.Token);
    }

    public void StartRelayMode(string multicastGroup = NetworkConfig.DefaultMulticastGroup, 
                              int multicastPort = NetworkConfig.DefaultMulticastPort)
    {
        Stop();

        _cts = new CancellationTokenSource();
        _udpClient = new UdpClient();
        _udpClient.JoinMulticastGroup(IPAddress.Parse(multicastGroup));
        _endpoint = new IPEndPoint(IPAddress.Parse(multicastGroup), multicastPort);
    }

    public async Task BroadcastExternalFrameAsync(byte[] frameBytes)
    {
        if (_udpClient == null || _endpoint == null || frameBytes == null || frameBytes.Length == 0) return;

        try
        {
            uint frameId = unchecked(++_frameIdCounter);
            ushort chunkCount = (ushort)Math.Ceiling((double)frameBytes.Length / ChunkDataSize);

            for (ushort i = 0; i < chunkCount; i++)
            {
                int offset = i * ChunkDataSize;
                int length = Math.Min(ChunkDataSize, frameBytes.Length - offset);

                byte[] packetBytes = new byte[BroadcastChunkHeader.HeaderSize + length];
                var header = new BroadcastChunkHeader(frameId, i, chunkCount);
                header.WriteTo(packetBytes.AsSpan(0, BroadcastChunkHeader.HeaderSize));

                Buffer.BlockCopy(frameBytes, offset, packetBytes, BroadcastChunkHeader.HeaderSize, length);
                await _udpClient.SendAsync(packetBytes, packetBytes.Length, _endpoint);
            }
        }
        catch { }
    }

    private async Task BroadcastLoopAsync(IPEndPoint endpoint, int intervalMs, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _udpClient != null)
        {
            try
            {
                byte[]? frameBytes = _capturer.CaptureFrameJpeg(quality: 85, targetWidth: 1920, targetHeight: 1080);
                if (frameBytes != null && frameBytes.Length > 0)
                {
                    uint frameId = unchecked(++_frameIdCounter);
                    ushort chunkCount = (ushort)Math.Ceiling((double)frameBytes.Length / ChunkDataSize);

                    for (ushort i = 0; i < chunkCount; i++)
                    {
                        int offset = i * ChunkDataSize;
                        int length = Math.Min(ChunkDataSize, frameBytes.Length - offset);

                        byte[] packetBytes = new byte[BroadcastChunkHeader.HeaderSize + length];
                        var header = new BroadcastChunkHeader(frameId, i, chunkCount);
                        header.WriteTo(packetBytes.AsSpan(0, BroadcastChunkHeader.HeaderSize));

                        Buffer.BlockCopy(frameBytes, offset, packetBytes, BroadcastChunkHeader.HeaderSize, length);
                        await _udpClient.SendAsync(packetBytes, packetBytes.Length, endpoint);
                    }
                }
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Bỏ qua lỗi gửi lẻ tẻ
            }

            try
            {
                await Task.Delay(intervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _udpClient?.Dispose();
        _udpClient = null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
        _cts?.Dispose();
    }
}
