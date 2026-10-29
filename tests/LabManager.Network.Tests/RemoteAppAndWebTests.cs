using System.Text.Json;
using LabManager.Core.Enums;
using LabManager.Native.System;
using LabManager.Protocol;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;
using LabManager.Student.Core.Services;
using LabManager.Teacher.Core.Services;

namespace LabManager.Network.Tests;

public class RemoteAppAndWebTests
{
    [Fact]
    public void OpenWebUrlPacket_SerializesAndDeserializes_Correctly()
    {
        var packet = new OpenWebUrlPacket
        {
            Url = "https://kahoot.it",
            ForceChrome = true
        };

        byte[] bytes = PacketSerializer.Serialize(CommandType.OpenWebUrl, packet);

        Assert.True(PacketSerializer.TryDeserializeHeader(bytes, out var header));
        Assert.Equal(CommandType.OpenWebUrl, header.Type);

        var deserialized = PacketSerializer.DeserializePayload<OpenWebUrlPacket>(bytes, PacketHeader.HeaderSize, header.PayloadLength);
        Assert.NotNull(deserialized);
        Assert.Equal("https://kahoot.it", deserialized.Url);
        Assert.True(deserialized.ForceChrome);
    }

    [Fact]
    public void LaunchAppPacket_SerializesAndDeserializes_Correctly()
    {
        var packet = new LaunchAppPacket
        {
            AppNameOrPath = "winword.exe",
            Arguments = "/safe"
        };

        byte[] bytes = PacketSerializer.Serialize(CommandType.LaunchApp, packet);

        Assert.True(PacketSerializer.TryDeserializeHeader(bytes, out var header));
        Assert.Equal(CommandType.LaunchApp, header.Type);

        var deserialized = PacketSerializer.DeserializePayload<LaunchAppPacket>(bytes, PacketHeader.HeaderSize, header.PayloadLength);
        Assert.NotNull(deserialized);
        Assert.Equal("winword.exe", deserialized.AppNameOrPath);
        Assert.Equal("/safe", deserialized.Arguments);
    }

    [Theory]
    [InlineData("google.com", "https://google.com")]
    [InlineData("kahoot.it", "https://kahoot.it")]
    [InlineData("http://example.com", "http://example.com")]
    [InlineData("https://quizizz.com", "https://quizizz.com")]
    [InlineData("", "https://www.google.com")]
    [InlineData("   ", "https://www.google.com")]
    public void BrowserLauncherHelper_NormalizesUrls_Properly(string input, string expected)
    {
        string normalized = BrowserLauncherHelper.NormalizeUrl(input);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("word", "winword.exe")]
    [InlineData("excel", "excel.exe")]
    [InlineData("powerpoint", "powerpnt.exe")]
    [InlineData("vscode", "code.exe")]
    [InlineData("code", "code.exe")]
    [InlineData("scratch", "scratch.exe")]
    [InlineData("calc", "calc.exe")]
    [InlineData("notepad", "notepad.exe")]
    [InlineData("notepad.exe", "notepad.exe")]
    [InlineData("   \"winword.exe\"  ", "winword.exe")]
    public void AppLauncherHelper_ResolvesAppAliases_Properly(string input, string expected)
    {
        string resolved = AppLauncherHelper.ResolveAppNameOrPath(input);
        Assert.Equal(expected, resolved);
    }

    [Fact]
    public async Task ClassroomManager_CanSend_OpenWebUrlCommand_ToStudent()
    {
        using var classroom = new ClassroomManager();
        using var student = new StudentCoordinator();

        var tcs = new TaskCompletionSource<bool>();

        student.Start();
        classroom.StartDiscovery(teacherName: "Thầy Hùng", roomName: "Phòng Tin Học 01");

        // Chờ nhận diện student
        await Task.Delay(1500);

        var studentNodes = classroom.GetAllStudents();
        Assert.NotEmpty(studentNodes);

        var targetStudent = studentNodes.First();

        // Gửi lệnh mở trang web
        bool success = await classroom.SendOpenUrlAsync(targetStudent.IpAddress, "https://kahoot.it", forceChrome: true);
        Assert.True(success);

        student.Stop();
    }

    [Fact]
    public async Task ClassroomManager_CanSend_LaunchAppCommand_ToStudent()
    {
        using var classroom = new ClassroomManager();
        using var student = new StudentCoordinator();

        student.Start();
        classroom.StartDiscovery(teacherName: "Thầy Hùng", roomName: "Phòng Tin Học 01");

        // Chờ nhận diện student
        await Task.Delay(1500);

        var studentNodes = classroom.GetAllStudents();
        Assert.NotEmpty(studentNodes);

        var targetStudent = studentNodes.First();

        // Gửi lệnh mở ứng dụng
        bool success = await classroom.SendLaunchAppAsync(targetStudent.IpAddress, "notepad.exe");
        Assert.True(success);

        student.Stop();
    }
}
