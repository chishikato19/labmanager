using System.Text;
using LabManager.Native.System;
using LabManager.Network.Tcp;
using LabManager.Protocol;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;
using LabManager.Student.Core.Handlers;
using LabManager.Teacher.Core.Services;

namespace LabManager.Network.Tests;

public class FileExchangeTests
{
    [Fact]
    public void FileZipHelper_CompressAndExtract_PreservesContent()
    {
        string sourceDir = Path.Combine(Path.GetTempPath(), "LabManagerTest_Src_" + Guid.NewGuid().ToString("N"));
        string targetDir = Path.Combine(Path.GetTempPath(), "LabManagerTest_Dst_" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(Path.Combine(sourceDir, "cau1.py"), "print('Hello Python')");
            File.WriteAllText(Path.Combine(sourceDir, "cau2.txt"), "Dap an bai tap 2");

            byte[] zipBytes = FileZipHelper.CreateZipFromDirectory(sourceDir);
            Assert.NotNull(zipBytes);
            Assert.True(zipBytes.Length > 0);

            FileZipHelper.ExtractZipToDirectory(zipBytes, targetDir);

            string extractedCau1 = Path.Combine(targetDir, "cau1.py");
            string extractedCau2 = Path.Combine(targetDir, "cau2.txt");

            Assert.True(File.Exists(extractedCau1));
            Assert.True(File.Exists(extractedCau2));
            Assert.Equal("print('Hello Python')", File.ReadAllText(extractedCau1));
            Assert.Equal("Dap an bai tap 2", File.ReadAllText(extractedCau2));
        }
        finally
        {
            try { if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, true); } catch { }
            try { if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true); } catch { }
        }
    }

    [Fact]
    public async Task EndToEnd_DistributeAndCollectAssignment_WorksOverTcp()
    {
        const int testFilePort = 9304;
        string tempTeacherFolder = Path.Combine(Path.GetTempPath(), "LabManagerTest_TeacherCol_" + Guid.NewGuid().ToString("N"));
        string tempStudentFolder = Path.Combine(Path.GetTempPath(), "LabManagerTest_Student_" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempStudentFolder);
        Directory.CreateDirectory(tempTeacherFolder);

        using var teacherService = new TeacherFileService();
        teacherService.StartFileReceiver(testFilePort);

        var studentHandler = new FileExchangeHandler();

        // 1. Tạo file bài làm tại máy học sinh
        string studentFile = Path.Combine(tempStudentFolder, "BaiLam.txt");
        await File.WriteAllTextAsync(studentFile, "Bai lam cua hoc sinh A");

        // 2. Giáo viên phát lệnh thu bài
        var tcs = new TaskCompletionSource<FileCollectResponsePacket>();
        teacherService.OnFileCollected += (submission, path) =>
        {
            tcs.TrySetResult(submission);
        };

        // Học sinh phản hồi đóng gói thư mục và gửi về giáo viên
        byte[] zipBytes = FileZipHelper.CreateZipFromDirectory(tempStudentFolder);
        var responsePacket = new FileCollectResponsePacket
        {
            MachineId = "TEST_PC",
            MachineName = "DESKTOP-HS01",
            StudentName = "NguyenVanA",
            ZipFileName = "DESKTOP-HS01_NguyenVanA.zip",
            ZipBytes = zipBytes
        };

        byte[] packetBytes = PacketSerializer.Serialize(LabManager.Core.Enums.CommandType.FileCollectResponse, responsePacket);
        var sender = new TeacherCommandSender();
        bool sent = await sender.SendCommandAsync("127.0.0.1", packetBytes, testFilePort);

        Assert.True(sent);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
        Assert.Equal(tcs.Task, completed);

        var received = await tcs.Task;
        Assert.Equal("DESKTOP-HS01", received.MachineName);
        Assert.Equal("NguyenVanA", received.StudentName);
        Assert.True(received.ZipBytes.Length > 0);

        try { if (Directory.Exists(tempStudentFolder)) Directory.Delete(tempStudentFolder, true); } catch { }
        try { if (Directory.Exists(tempTeacherFolder)) Directory.Delete(tempTeacherFolder, true); } catch { }
    }
}
