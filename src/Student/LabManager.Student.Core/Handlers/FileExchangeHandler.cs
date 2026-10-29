using System.IO.Compression;
using System.Text.RegularExpressions;
using LabManager.Core.Enums;
using LabManager.Native.System;
using LabManager.Network.Tcp;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;

namespace LabManager.Student.Core.Handlers;

public class FileExchangeHandler
{
    private readonly TeacherCommandSender _sender = new();

    public string GetDefaultStudentFolder(string subFolder = "BaiTap")
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string folder = Path.Combine(desktop, subFolder);
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        return folder;
    }

    public async Task HandleDistributeAsync(FileDistributePacket packet)
    {
        string targetDir = GetDefaultStudentFolder(packet.TargetSubFolder);

        if (packet.AutoExtract && packet.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            FileZipHelper.ExtractZipToDirectory(packet.FileBytes, targetDir);
        }
        else
        {
            string filePath = Path.Combine(targetDir, packet.FileName);
            await File.WriteAllBytesAsync(filePath, packet.FileBytes);
        }
    }

    public async Task<bool> HandleCollectRequestAsync(FileCollectRequestPacket request, string machineId)
    {
        if (string.IsNullOrWhiteSpace(request.TeacherIp)) return false;

        // Nếu yêu cầu tự động thu bài từ thư mục Desktop\BaiTap
        if (request.AutoCollectDesktop)
        {
            string sourceDir = GetDefaultStudentFolder(request.SourceSubFolder);
            byte[] zipBytes = FileZipHelper.CreateZipFromDirectory(sourceDir);

            var response = new FileCollectResponsePacket
            {
                MachineId = machineId,
                MachineName = Environment.MachineName,
                StudentName = Environment.UserName,
                ZipFileName = $"{Environment.MachineName}_{Environment.UserName}.zip",
                ZipBytes = zipBytes,
                SubmittedAt = DateTime.UtcNow
            };

            byte[] packetBytes = PacketSerializer.Serialize(CommandType.FileCollectResponse, response);
            return await _sender.SendCommandAsync(request.TeacherIp, packetBytes, request.TeacherFilePort);
        }

        return true;
    }

    public async Task<bool> SubmitFilesAsync(
        string teacherIp,
        int teacherFilePort,
        string machineId,
        string studentName,
        string note,
        IReadOnlyList<string> filePaths,
        IProgress<(int current, int total, string fileName)>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(teacherIp) || filePaths == null || filePaths.Count == 0)
        {
            return false;
        }

        var validFiles = filePaths.Where(File.Exists).ToList();
        if (validFiles.Count == 0)
        {
            return false;
        }

        string safeStudentName = string.IsNullOrWhiteSpace(studentName) ? Environment.UserName : studentName.Trim();
        bool allSuccess = true;
        int count = 0;

        foreach (var file in validFiles)
        {
            count++;
            string fileName = Path.GetFileName(file);
            progress?.Report((count, validFiles.Count, fileName));

            byte[] fileBytes = await File.ReadAllBytesAsync(file);

            var response = new FileCollectResponsePacket
            {
                MachineId = machineId,
                MachineName = Environment.MachineName,
                StudentName = safeStudentName,
                OriginalFileName = fileName,
                ZipFileName = fileName, // Giữ nguyên tên file gốc, không kèm tên học sinh
                ZipBytes = fileBytes,
                SubmittedAt = DateTime.UtcNow,
                Note = note ?? string.Empty,
                FileSizeBytes = fileBytes.Length
            };

            byte[] bytes = PacketSerializer.Serialize(CommandType.FileCollectResponse, response);
            bool sent = await _sender.SendCommandAsync(teacherIp, bytes, teacherFilePort);
            if (!sent)
            {
                allSuccess = false;
            }
        }

        return allSuccess;
    }
}
