using System.Net;
using System.Net.Sockets;
using LabManager.Core.Models;
using LabManager.Protocol.Packets;

namespace LabManager.Network.Streaming;

public class ScreenBroadcastReceiver : IDisposable
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private IPAddress? _multicastGroupIp;
    private bool _isDisposed;

    // Buffer ghép khung hình
    private uint _currentFrameId;
    private byte[][]? _chunkBuffers;
    private int _receivedChunks;
    private int _totalExpectedLength;

    public event Action<byte[]>? OnFrameReceived;

    public bool IsListening => _cts is { IsCancellationRequested: false };

    public void Start(string multicastGroup = NetworkConfig.DefaultMulticastGroup, 
                      int multicastPort = NetworkConfig.DefaultMulticastPort)
    {
        if (IsListening) return;

        _cts = new CancellationTokenSource();
        _multicastGroupIp = IPAddress.Parse(multicastGroup);

        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.ReceiveBufferSize = 4 * 1024 * 1024; // 4MB buffer cho frame 1080p
        socket.Bind(new IPEndPoint(IPAddress.Any, multicastPort));

        _udpClient = new UdpClient { Client = socket };
        _udpClient.JoinMulticastGroup(_multicastGroupIp);

        _ = ReceiveLoopAsync(_cts.Token);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _udpClient != null)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(ct);
                if (result.Buffer.Length < BroadcastChunkHeader.HeaderSize)
                    continue;

                if (!BroadcastChunkHeader.TryParse(result.Buffer.AsSpan(0, BroadcastChunkHeader.HeaderSize), out var header))
                    continue;

                ProcessChunk(header, result.Buffer, BroadcastChunkHeader.HeaderSize, result.Buffer.Length - BroadcastChunkHeader.HeaderSize);
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Bỏ qua lỗi nhận lẻ tẻ
            }
        }
    }

    private void ProcessChunk(BroadcastChunkHeader header, byte[] buffer, int offset, int length)
    {
        // Khung hình mới
        if (header.FrameId != _currentFrameId)
        {
            _currentFrameId = header.FrameId;
            _chunkBuffers = new byte[header.ChunkCount][];
            _receivedChunks = 0;
            _totalExpectedLength = 0;
        }

        if (_chunkBuffers == null || header.ChunkIndex >= _chunkBuffers.Length)
            return;

        if (_chunkBuffers[header.ChunkIndex] == null)
        {
            byte[] chunkData = new byte[length];
            Buffer.BlockCopy(buffer, offset, chunkData, 0, length);
            _chunkBuffers[header.ChunkIndex] = chunkData;

            _receivedChunks++;
            _totalExpectedLength += length;

            // Đã nhận đủ tất cả các phần của khung hình
            if (_receivedChunks == header.ChunkCount)
            {
                byte[] completeFrame = new byte[_totalExpectedLength];
                int writePos = 0;

                for (int i = 0; i < header.ChunkCount; i++)
                {
                    var chunk = _chunkBuffers[i];
                    if (chunk != null)
                    {
                        Buffer.BlockCopy(chunk, 0, completeFrame, writePos, chunk.Length);
                        writePos += chunk.Length;
                    }
                }

                OnFrameReceived?.Invoke(completeFrame);
            }
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        if (_udpClient != null && _multicastGroupIp != null)
        {
            try { _udpClient.DropMulticastGroup(_multicastGroupIp); } catch { }
        }
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
