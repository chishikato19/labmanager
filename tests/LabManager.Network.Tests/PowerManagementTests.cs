using LabManager.Core.Enums;
using LabManager.Native.System;
using LabManager.Network.Tcp;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;
using LabManager.Student.Core.Handlers;

namespace LabManager.Network.Tests;

public class PowerManagementTests
{
    [Fact]
    public void BuildMagicPacket_ValidMac_Generates102BytesPayload()
    {
        string mac = "00:1A:2B:3C:4D:5E";
        byte[] packet = WakeOnLanHelper.BuildMagicPacket(mac);

        Assert.Equal(102, packet.Length);
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(0xFF, packet[i]);
        }

        byte[] expectedMacBytes = [0x00, 0x1A, 0x2B, 0x3C, 0x4D, 0x5E];
        for (int rep = 0; rep < 16; rep++)
        {
            int offset = 6 + (rep * 6);
            for (int b = 0; b < 6; b++)
            {
                Assert.Equal(expectedMacBytes[b], packet[offset + b]);
            }
        }
    }

    [Theory]
    [InlineData("12:34:56")]
    [InlineData("invalid-mac-address")]
    [InlineData("")]
    public void BuildMagicPacket_InvalidMac_ThrowsArgumentException(string invalidMac)
    {
        Assert.Throws<ArgumentException>(() => WakeOnLanHelper.BuildMagicPacket(invalidMac));
    }

    [Fact]
    public void PowerController_GetCommandArguments_GeneratesCorrectParameters()
    {
        var (fileShutdown, argsShutdown) = PowerController.GetCommandArguments(PowerAction.Shutdown, force: true, delaySeconds: 0);
        Assert.Equal("shutdown.exe", fileShutdown);
        Assert.Contains("/s", argsShutdown);
        Assert.Contains("/f", argsShutdown);

        var (fileRestart, argsRestart) = PowerController.GetCommandArguments(PowerAction.Restart, force: true, delaySeconds: 5);
        Assert.Equal("shutdown.exe", fileRestart);
        Assert.Contains("/r", argsRestart);
        Assert.Contains("/t 5", argsRestart);

        var (fileSleep, argsSleep) = PowerController.GetCommandArguments(PowerAction.Sleep, force: false, delaySeconds: 0);
        Assert.Equal("rundll32.exe", fileSleep);
        Assert.Contains("SetSuspendState", argsSleep);
    }

    [Fact]
    public async Task TeacherAndStudent_CanSendAndHandlePowerCommand_OverTcp()
    {
        const int testPort = 9205;
        using var listener = new TcpCommandListener();
        var handler = new PowerControlHandler();
        var sender = new TeacherCommandSender();

        var tcs = new TaskCompletionSource<PowerControlPacket>();
        handler.CustomExecutor = cmd => tcs.TrySetResult(cmd);

        listener.OnCommandReceived += async (header, payload) =>
        {
            if (header.Type == CommandType.PowerControl)
            {
                var cmd = PacketSerializer.DeserializePayload<PowerControlPacket>(payload, 0, payload.Length);
                if (cmd != null)
                {
                    await handler.HandleAsync(cmd);
                }
            }
        };

        listener.Start(testPort);

        var powerPacket = new PowerControlPacket
        {
            Action = PowerAction.Restart,
            Force = true,
            DelaySeconds = 10,
            Message = "Bảo trì định kỳ"
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.PowerControl, powerPacket);

        bool sent = await sender.SendCommandAsync("127.0.0.1", bytes, testPort);
        Assert.True(sent);

        var received = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        Assert.Same(tcs.Task, received);

        var result = await tcs.Task;
        Assert.Equal(PowerAction.Restart, result.Action);
        Assert.True(result.Force);
        Assert.Equal(10, result.DelaySeconds);
        Assert.Equal("Bảo trì định kỳ", result.Message);
    }
}
