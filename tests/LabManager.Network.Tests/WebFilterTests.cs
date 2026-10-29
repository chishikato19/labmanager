using LabManager.Core.Enums;
using LabManager.Native.System;
using LabManager.Network.Tcp;
using LabManager.Protocol;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;
using LabManager.Student.Core.Handlers;

namespace LabManager.Network.Tests;

public class WebFilterTests
{
    [Fact]
    public void HostsFileHelper_ApplyAndClearBlockedDomains_UpdatesFileSafely()
    {
        string tempHosts = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempHosts, "127.0.0.1 localhost\n::1 localhost\n");
            HostsFileHelper.HostsFilePath = tempHosts;

            // 1. Áp dụng chặn
            HostsFileHelper.ApplyBlockedDomains(["facebook.com", "https://tiktok.com/watch"]);
            string contentAfterBlock = File.ReadAllText(tempHosts);

            Assert.Contains("127.0.0.1 facebook.com", contentAfterBlock);
            Assert.Contains("127.0.0.1 www.facebook.com", contentAfterBlock);
            Assert.Contains("127.0.0.1 tiktok.com", contentAfterBlock);

            // 2. Gỡ bỏ chặn
            HostsFileHelper.ClearBlockedDomains();
            string contentAfterClear = File.ReadAllText(tempHosts);

            Assert.DoesNotContain("facebook.com", contentAfterClear);
            Assert.DoesNotContain("tiktok.com", contentAfterClear);
            Assert.Contains("127.0.0.1 localhost", contentAfterClear);
        }
        finally
        {
            if (File.Exists(tempHosts)) File.Delete(tempHosts);
        }
    }

    [Fact]
    public void WebFilterControlPacket_Serialization_PreservesValues()
    {
        var packet = new WebFilterControlPacket
        {
            Action = WebFilterAction.SetDomainBlacklist,
            BlacklistedDomains = ["youtube.com", "roblox.com"],
            BlockAllBrowsing = true
        };

        byte[] bytes = PacketSerializer.Serialize(CommandType.WebFilterControl, packet);
        Assert.True(PacketSerializer.TryDeserializeHeader(bytes, out var header));
        Assert.Equal(CommandType.WebFilterControl, header.Type);

        var deserialized = PacketSerializer.DeserializePayload<WebFilterControlPacket>(bytes, PacketHeader.HeaderSize, header.PayloadLength);
        Assert.NotNull(deserialized);
        Assert.Equal(WebFilterAction.SetDomainBlacklist, deserialized.Action);
        Assert.True(deserialized.BlockAllBrowsing);
        Assert.Equal(2, deserialized.BlacklistedDomains.Count);
    }

    [Fact]
    public async Task TeacherAndStudent_CanSendWebFilterCommand_OverTcp()
    {
        const int testPort = 9207;
        using var listener = new TcpCommandListener();
        using var handler = new WebFilterHandler("TEST_STUDENT_WEB");
        var sender = new TeacherCommandSender();

        var tcs = new TaskCompletionSource<WebActivityReportPacket>();
        handler.OnWebActivityReady += report =>
        {
            tcs.TrySetResult(report);
            return Task.CompletedTask;
        };

        listener.OnCommandReceived += async (header, payload) =>
        {
            if (header.Type == CommandType.WebFilterControl)
            {
                var cmd = PacketSerializer.DeserializePayload<WebFilterControlPacket>(payload, 0, payload.Length);
                if (cmd != null)
                {
                    await handler.HandleAsync(cmd);
                }
            }
        };

        listener.Start(testPort);

        var requestPacket = new WebFilterControlPacket
        {
            Action = WebFilterAction.GetWebActivity
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.WebFilterControl, requestPacket);

        bool sent = await sender.SendCommandAsync("127.0.0.1", bytes, testPort);
        Assert.True(sent);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        Assert.Same(tcs.Task, completed);

        var result = await tcs.Task;
        Assert.NotNull(result);
        Assert.Equal("TEST_STUDENT_WEB", result.MachineId);
    }
}
