using LabManager.Core.Enums;
using LabManager.Protocol;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;
using LabManager.Teacher.Core.Services;

namespace LabManager.Network.Tests;

public class WallpaperAndDeployTests
{
    [Fact]
    public void SetWallpaperPacket_SerializeAndDeserialize_PreservesData()
    {
        var packet = new SetWallpaperPacket
        {
            FileName = "school_wallpaper.jpg",
            ImageBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46],
            Style = 10
        };

        byte[] bytes = PacketSerializer.Serialize(CommandType.SetWallpaper, packet);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > PacketHeader.HeaderSize);

        bool headerParsed = PacketSerializer.TryDeserializeHeader(bytes, out var header);
        Assert.True(headerParsed);
        Assert.Equal(CommandType.SetWallpaper, header.Type);

        var deserialized = PacketSerializer.DeserializePayload<SetWallpaperPacket>(bytes, PacketHeader.HeaderSize, header.PayloadLength);
        Assert.NotNull(deserialized);
        Assert.Equal(packet.FileName, deserialized.FileName);
        Assert.Equal(packet.Style, deserialized.Style);
        Assert.Equal(packet.ImageBytes, deserialized.ImageBytes);
    }

    [Fact]
    public void DeploySoftwarePacket_SerializeAndDeserialize_PreservesData()
    {
        var packet = new DeploySoftwarePacket
        {
            DownloadUrl = "http://192.168.1.10:9007/vscode_setup.exe",
            FileName = "vscode_setup.exe",
            Arguments = "/VERYSILENT /NORESTART",
            Sha256 = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
            IsMsi = false
        };

        byte[] bytes = PacketSerializer.Serialize(CommandType.DeploySoftware, packet);
        Assert.NotNull(bytes);

        bool headerParsed = PacketSerializer.TryDeserializeHeader(bytes, out var header);
        Assert.True(headerParsed);
        Assert.Equal(CommandType.DeploySoftware, header.Type);

        var deserialized = PacketSerializer.DeserializePayload<DeploySoftwarePacket>(bytes, PacketHeader.HeaderSize, header.PayloadLength);
        Assert.NotNull(deserialized);
        Assert.Equal(packet.DownloadUrl, deserialized.DownloadUrl);
        Assert.Equal(packet.FileName, deserialized.FileName);
        Assert.Equal(packet.Arguments, deserialized.Arguments);
        Assert.Equal(packet.Sha256, deserialized.Sha256);
        Assert.Equal(packet.IsMsi, deserialized.IsMsi);
    }

    [Fact]
    public void DeploySoftwareStatusPacket_SerializeAndDeserialize_PreservesData()
    {
        var packet = new DeploySoftwareStatusPacket
        {
            MachineId = "TEST-NODE-01",
            MachineName = "STUDENT-PC-01",
            Stage = "Completed",
            ProgressPercentage = 100,
            ExitCode = 0,
            Message = "Cài đặt thành công."
        };

        byte[] bytes = PacketSerializer.Serialize(CommandType.DeploySoftwareStatus, packet);
        Assert.NotNull(bytes);

        bool headerParsed = PacketSerializer.TryDeserializeHeader(bytes, out var header);
        Assert.True(headerParsed);
        Assert.Equal(CommandType.DeploySoftwareStatus, header.Type);

        var deserialized = PacketSerializer.DeserializePayload<DeploySoftwareStatusPacket>(bytes, PacketHeader.HeaderSize, header.PayloadLength);
        Assert.NotNull(deserialized);
        Assert.Equal(packet.MachineId, deserialized.MachineId);
        Assert.Equal(packet.MachineName, deserialized.MachineName);
        Assert.Equal(packet.Stage, deserialized.Stage);
        Assert.Equal(packet.ProgressPercentage, deserialized.ProgressPercentage);
        Assert.Equal(packet.ExitCode, deserialized.ExitCode);
        Assert.Equal(packet.Message, deserialized.Message);
    }

    [Fact]
    public async Task LanDeployServer_ServesFileOverHttp_AndMatchesSha256()
    {
        int testPort = 19007;
        string tempFile = Path.Combine(Path.GetTempPath(), $"TestDeploy_{Guid.NewGuid():N}.msi");
        byte[] sampleBytes = new byte[1024 * 32]; // 32 KB
        new Random(42).NextBytes(sampleBytes);
        await File.WriteAllBytesAsync(tempFile, sampleBytes);

        using var server = new LanDeployServer();
        try
        {
            server.Start(tempFile, testPort);

            string expectedSha256 = await LanDeployServer.ComputeFileSha256Async(tempFile);
            Assert.False(string.IsNullOrEmpty(expectedSha256));

            using var httpClient = new HttpClient();
            byte[] downloadedBytes = await httpClient.GetByteArrayAsync($"http://127.0.0.1:{testPort}/test.msi");

            Assert.Equal(sampleBytes.Length, downloadedBytes.Length);
            Assert.Equal(sampleBytes, downloadedBytes);
        }
        finally
        {
            server.Stop();
            try { File.Delete(tempFile); } catch { }
        }
    }
}
