using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using LabManager.Core.Enums;
using LabManager.Core.Models;
using LabManager.Protocol;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;

namespace LabManager.Network.Streaming;

public class TeacherScreenReceiver : IDisposable
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private bool _isDisposed;

    private readonly ConcurrentDictionary<string, FrameReassembly> _buffers = new();

    public event Action<ScreenFramePacket, string>? OnFrameReceived;

    public bool IsListening => _cts is { IsCancellationRequested: false };

    public void Start(int listenPort = NetworkConfig.DefaultStreamPort)
    {
        if (IsListening) return;

        _cts = new CancellationTokenSource();

        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.ReceiveBufferSize = 4 * 1024 * 1024; // 4MB buffer cho stream nhiều máy
        socket.Bind(new IPEndPoint(IPAddress.Any, listenPort));

        _udpClient = new UdpClient { Client = socket };

        _ = ReceiveLoopAsync(_cts.Token);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _udpClient != null)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(ct);
                if (result.Buffer.Length < 12)
                    continue;

                string senderIp = result.RemoteEndPoint.Address.ToString();

                // 1. Kiểm tra gói chia mảnh tốc độ cao (ScreenChunkHeader)
                if (result.Buffer.Length >= ScreenChunkHeader.HeaderSize &&
                    ScreenChunkHeader.TryParse(result.Buffer.AsSpan(0, ScreenChunkHeader.HeaderSize), out var chunkHeader))
                {
                    int chunkDataLen = result.Buffer.Length - ScreenChunkHeader.HeaderSize;
                    ProcessScreenChunk(chunkHeader, result.Buffer, ScreenChunkHeader.HeaderSize, chunkDataLen, senderIp);
                    continue;
                }

                // 2. Tương thích ngược: Gói tin cũ qua PacketSerializer
                if (PacketSerializer.TryDeserializeHeader(result.Buffer, out var header) &&
                    header.Type == CommandType.ScreenFrame)
                {
                    var packet = PacketSerializer.DeserializePayload<ScreenFramePacket>(
                        result.Buffer, PacketHeader.HeaderSize, header.PayloadLength);

                    if (packet != null)
                    {
                        OnFrameReceived?.Invoke(packet, senderIp);
                    }
                }
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Bỏ qua lỗi nhận khung hình lẻ
            }
        }
    }

    private void ProcessScreenChunk(ScreenChunkHeader header, byte[] buffer, int offset, int length, string senderIp)
    {
        string machineKey = header.MachineId.ToString("N");
        var reassembly = _buffers.GetOrAdd(machineKey, _ => new FrameReassembly());

        lock (reassembly)
        {
            // Nếu là frame mới hơn
            if (header.FrameId != reassembly.FrameId)
            {
                // Bỏ qua các frame cũ đến muộn do độ trễ UDP
                if (reassembly.FrameId != 0 && header.FrameId < reassembly.FrameId && (reassembly.FrameId - header.FrameId) < 100)
                    return;

                reassembly.FrameId = header.FrameId;
                reassembly.Chunks = new byte[header.ChunkCount][];
                reassembly.ReceivedChunks = 0;
                reassembly.TotalLength = 0;
                reassembly.Width = header.Width;
                reassembly.Height = header.Height;
            }

            if (reassembly.Chunks == null || header.ChunkIndex >= reassembly.Chunks.Length)
                return;

            if (reassembly.Chunks[header.ChunkIndex] == null)
            {
                byte[] chunkData = new byte[length];
                Buffer.BlockCopy(buffer, offset, chunkData, 0, length);
                reassembly.Chunks[header.ChunkIndex] = chunkData;

                reassembly.ReceivedChunks++;
                reassembly.TotalLength += length;

                // Khi đã gom đủ tất cả các mảnh của khung hình
                if (reassembly.ReceivedChunks == header.ChunkCount)
                {
                    byte[] completeJpeg = new byte[reassembly.TotalLength];
                    int writePos = 0;

                    for (int i = 0; i < header.ChunkCount; i++)
                    {
                        var chunk = reassembly.Chunks[i];
                        if (chunk != null)
                        {
                            Buffer.BlockCopy(chunk, 0, completeJpeg, writePos, chunk.Length);
                            writePos += chunk.Length;
                        }
                    }

                    var packet = new ScreenFramePacket
                    {
                        MachineId = machineKey,
                        Width = reassembly.Width,
                        Height = reassembly.Height,
                        ImageBytes = completeJpeg,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };

                    OnFrameReceived?.Invoke(packet, senderIp);
                }
            }
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _udpClient?.Dispose();
        _udpClient = null;
        _buffers.Clear();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
        _cts?.Dispose();
    }

    private class FrameReassembly
    {
        public uint FrameId;
        public byte[][]? Chunks;
        public int ReceivedChunks;
        public int TotalLength;
        public ushort Width;
        public ushort Height;
    }
}
