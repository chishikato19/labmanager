using LabManager.Core.Enums;
using LabManager.Native.System;
using LabManager.Network.Tcp;
using LabManager.Protocol;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;
using LabManager.Student.Core.Handlers;

namespace LabManager.Network.Tests;

public class ProcessManagementTests
{
    [Fact]
    public void ProcessHelper_GetActiveProcesses_ReturnsCurrentProcesses()
    {
        var processes = ProcessHelper.GetActiveProcesses(windowedOnly: false);

        Assert.NotNull(processes);
        Assert.NotEmpty(processes);
        Assert.Contains(processes, p => p.Pid > 0 && !string.IsNullOrWhiteSpace(p.ProcessName));
    }

    [Fact]
    public void ProcessControlPacket_Serialization_PreservesValues()
    {
        var packet = new ProcessControlPacket
        {
            Action = ProcessAction.SetBlacklist,
            TargetPid = 1234,
            TargetProcessName = "cheatengine.exe",
            Blacklist = ["game.exe", "discord.exe", "roblox.exe"]
        };

        byte[] bytes = PacketSerializer.Serialize(CommandType.ProcessControl, packet);
        Assert.True(PacketSerializer.TryDeserializeHeader(bytes, out var header));
        Assert.Equal(CommandType.ProcessControl, header.Type);

        var deserialized = PacketSerializer.DeserializePayload<ProcessControlPacket>(bytes, PacketHeader.HeaderSize, header.PayloadLength);
        Assert.NotNull(deserialized);
        Assert.Equal(ProcessAction.SetBlacklist, deserialized.Action);
        Assert.Equal(1234, deserialized.TargetPid);
        Assert.Equal("cheatengine.exe", deserialized.TargetProcessName);
        Assert.Equal(3, deserialized.Blacklist.Count);
    }

    [Fact]
    public void ProcessMonitor_CanSetAndClearBlacklist()
    {
        using var monitor = new ProcessMonitor();
        monitor.SetBlacklist(["discord.exe", "GAME.EXE", "steam"]);
        // Đảm bảo không ném lỗi khi set và clear
        monitor.ClearBlacklist();
    }

    [Fact]
    public async Task TeacherAndStudent_CanSendProcessControlCommand_OverTcp()
    {
        const int testPort = 9206;
        using var listener = new TcpCommandListener();
        using var handler = new ProcessControlHandler("TEST_MACHINE_1");
        var sender = new TeacherCommandSender();

        var tcs = new TaskCompletionSource<ProcessListResponsePacket>();
        handler.OnProcessListReady += response =>
        {
            tcs.TrySetResult(response);
            return Task.CompletedTask;
        };

        listener.OnCommandReceived += async (header, payload) =>
        {
            if (header.Type == CommandType.ProcessControl)
            {
                var cmd = PacketSerializer.DeserializePayload<ProcessControlPacket>(payload, 0, payload.Length);
                if (cmd != null)
                {
                    await handler.HandleAsync(cmd);
                }
            }
        };

        listener.Start(testPort);

        var requestPacket = new ProcessControlPacket
        {
            Action = ProcessAction.GetRunningProcesses
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.ProcessControl, requestPacket);

        bool sent = await sender.SendCommandAsync("127.0.0.1", bytes, testPort);
        Assert.True(sent);

        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        Assert.Same(tcs.Task, completedTask);

        var result = await tcs.Task;
        Assert.NotNull(result);
        Assert.Equal("TEST_MACHINE_1", result.MachineId);
        Assert.NotEmpty(result.Processes);
    }
}
