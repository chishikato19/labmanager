using System.Net;
using System.Net.Sockets;
using LabManager.Network.Streaming;
using LabManager.Protocol.Packets;

namespace LabManager.Network.Tests;

public class BroadcastTests
{
    [Fact]
    public void BroadcastChunkHeader_SerializeAndParse_MatchesExactly()
    {
        var header = new BroadcastChunkHeader(frameId: 42, chunkIndex: 3, chunkCount: 10);
        Span<byte> buffer = stackalloc byte[BroadcastChunkHeader.HeaderSize];

        header.WriteTo(buffer);

        bool success = BroadcastChunkHeader.TryParse(buffer, out var parsed);

        Assert.True(success);
        Assert.Equal(42u, parsed.FrameId);
        Assert.Equal(3, parsed.ChunkIndex);
        Assert.Equal(10, parsed.ChunkCount);
    }

    [Fact]
    public async Task ScreenBroadcastReceiver_CanReassembleMultiChunkFrame_OverMulticast()
    {
        const string group = "239.255.42.99";
        const int port = 9103;

        using var receiver = new ScreenBroadcastReceiver();
        byte[]? receivedFrame = null;
        var tcs = new TaskCompletionSource<byte[]>();

        receiver.OnFrameReceived += frame =>
        {
            receivedFrame = frame;
            tcs.TrySetResult(frame);
        };

        receiver.Start(group, port);

        // Tạo dữ liệu giả lập 25,000 bytes (> 3 chunks x 8192 bytes)
        byte[] originalFrame = new byte[25000];
        new Random(42).NextBytes(originalFrame);

        // Gửi các chunks qua Multicast
        using var sender = new UdpClient();
        sender.JoinMulticastGroup(IPAddress.Parse(group));
        var endpoint = new IPEndPoint(IPAddress.Parse(group), port);

        const int chunkSize = 8192;
        ushort totalChunks = (ushort)Math.Ceiling((double)originalFrame.Length / chunkSize);
        uint frameId = 1001;

        for (ushort i = 0; i < totalChunks; i++)
        {
            int offset = i * chunkSize;
            int length = Math.Min(chunkSize, originalFrame.Length - offset);

            byte[] chunkPacket = new byte[BroadcastChunkHeader.HeaderSize + length];
            var header = new BroadcastChunkHeader(frameId, i, totalChunks);
            header.WriteTo(chunkPacket.AsSpan(0, BroadcastChunkHeader.HeaderSize));

            Buffer.BlockCopy(originalFrame, offset, chunkPacket, BroadcastChunkHeader.HeaderSize, length);
            await sender.SendAsync(chunkPacket, chunkPacket.Length, endpoint);
            await Task.Delay(10); // Giãn nhẹ giữa các chunks
        }

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
        Assert.Equal(tcs.Task, completed);

        Assert.NotNull(receivedFrame);
        Assert.Equal(originalFrame.Length, receivedFrame.Length);
        Assert.Equal(originalFrame, receivedFrame);
    }
}
