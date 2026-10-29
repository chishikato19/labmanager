using LabManager.Native.Screen;
using LabManager.Network.Streaming;
using LabManager.Protocol.Packets;

namespace LabManager.Network.Tests;

public class ScreenStreamTests
{
    [Fact]
    public void GdiScreenCapturer_CapturesValidJpegFrame()
    {
        using var capturer = new GdiScreenCapturer();
        capturer.Initialize();

        byte[]? frame = capturer.CaptureFrameJpeg(quality: 65, targetWidth: 640, targetHeight: 360);

        Assert.NotNull(frame);
        Assert.True(frame.Length > 100);

        // Kiểm tra JPEG magic header: FF D8 FF
        Assert.Equal(0xFF, frame[0]);
        Assert.Equal(0xD8, frame[1]);
        Assert.Equal(0xFF, frame[2]);
    }

    [Fact]
    public async Task StudentSender_CanStreamHighResFrameToTeacherReceiver_OverUdpLoopback()
    {
        const int testStreamPort = 9102;
        string testMachineId = Guid.NewGuid().ToString("N");

        using var capturer = new GdiScreenCapturer();
        using var receiver = new TeacherScreenReceiver();
        using var sender = new StudentScreenSender(capturer, machineId: testMachineId);

        ScreenFramePacket? receivedPacket = null;
        var tcs = new TaskCompletionSource<ScreenFramePacket>();

        receiver.OnFrameReceived += (packet, ip) =>
        {
            receivedPacket = packet;
            tcs.TrySetResult(packet);
        };

        receiver.Start(testStreamPort);
        sender.Start("127.0.0.1", testStreamPort, targetFps: 3);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
        Assert.Equal(tcs.Task, completed);

        Assert.NotNull(receivedPacket);
        Assert.Equal(testMachineId, receivedPacket.MachineId);
        Assert.Equal(640, receivedPacket.Width);
        Assert.Equal(360, receivedPacket.Height);
        Assert.True(receivedPacket.ImageBytes.Length > 0);
    }

    [Fact]
    public async Task StudentSender_RemoteControlMode_Streams720pFrameWithoutOverflow()
    {
        const int testStreamPort = 9103;
        string testMachineId = Guid.NewGuid().ToString("N");

        using var capturer = new GdiScreenCapturer();
        using var receiver = new TeacherScreenReceiver();
        using var sender = new StudentScreenSender(capturer, machineId: testMachineId);

        sender.SetRemoteControlMode(true);

        ScreenFramePacket? receivedPacket = null;
        var tcs = new TaskCompletionSource<ScreenFramePacket>();

        receiver.OnFrameReceived += (packet, ip) =>
        {
            receivedPacket = packet;
            tcs.TrySetResult(packet);
        };

        receiver.Start(testStreamPort);
        sender.Start("127.0.0.1", testStreamPort, targetFps: 5);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
        Assert.Equal(tcs.Task, completed);

        Assert.NotNull(receivedPacket);
        Assert.Equal(testMachineId, receivedPacket.MachineId);
        Assert.True(receivedPacket.Width >= 1280, $"Expected Width >= 1280, got {receivedPacket.Width}");
        Assert.True(receivedPacket.Height >= 720, $"Expected Height >= 720, got {receivedPacket.Height}");
        Assert.True(receivedPacket.ImageBytes.Length > 0);
    }
}
