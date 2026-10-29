using LabManager.Core.Enums;
using LabManager.Network.Tcp;
using LabManager.Protocol;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;
using LabManager.Student.Core.Handlers;

namespace LabManager.Network.Tests;

public class LockControlTests
{
    [Fact]
    public async Task TeacherCommandSender_CanLockAndUnlockStudent_OverTcp()
    {
        const int testPort = 9201;
        using var listener = new TcpCommandListener();
        using var lockHandler = new LockMachineHandler();
        var sender = new TeacherCommandSender();

        bool? lastLockedState = null;
        string? lastMessage = null;
        var lockTcs = new TaskCompletionSource<bool>();
        var unlockTcs = new TaskCompletionSource<bool>();

        lockHandler.OnLockStateChanged += (locked, msg) =>
        {
            lastLockedState = locked;
            lastMessage = msg;
            if (locked)
                lockTcs.TrySetResult(true);
            else
                unlockTcs.TrySetResult(true);
        };

        listener.OnCommandReceived += async (header, payload) =>
        {
            if (header.Type == CommandType.LockControl)
            {
                var cmd = PacketSerializer.DeserializePayload<LockControlPacket>(payload, 0, payload.Length);
                if (cmd != null)
                {
                    await lockHandler.HandleAsync(cmd);
                }
            }
        };

        listener.Start(testPort);

        // 1. Gửi lệnh KHÓA máy
        var lockPacket = new LockControlPacket
        {
            LockScreen = true,
            BlockInput = true,
            Message = "Lớp học đang bắt đầu làm bài!"
        };
        byte[] lockBytes = PacketSerializer.Serialize(CommandType.LockControl, lockPacket);
        bool sentLock = await sender.SendCommandAsync("127.0.0.1", lockBytes, testPort);

        Assert.True(sentLock);

        var lockCompleted = await Task.WhenAny(lockTcs.Task, Task.Delay(4000));
        Assert.Equal(lockTcs.Task, lockCompleted);
        Assert.True(lockHandler.IsLocked);
        Assert.True(lastLockedState);
        Assert.Equal("Lớp học đang bắt đầu làm bài!", lastMessage);

        // 2. Gửi lệnh MỞ KHÓA máy
        var unlockPacket = new LockControlPacket
        {
            LockScreen = false,
            BlockInput = false,
            Message = string.Empty
        };
        byte[] unlockBytes = PacketSerializer.Serialize(CommandType.LockControl, unlockPacket);
        bool sentUnlock = await sender.SendCommandAsync("127.0.0.1", unlockBytes, testPort);

        Assert.True(sentUnlock);

        var unlockCompleted = await Task.WhenAny(unlockTcs.Task, Task.Delay(4000));
        Assert.Equal(unlockTcs.Task, unlockCompleted);
        Assert.False(lockHandler.IsLocked);
        Assert.False(lastLockedState);
    }
}
