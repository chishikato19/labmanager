using LabManager.Core.Enums;
using LabManager.Protocol;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;
using LabManager.Teacher.Core.Services;

namespace LabManager.Network.Tests;

public class AppUpdateTests
{
    [Fact]
    public void AppUpdatePacket_SerializeAndDeserialize_PreservesData()
    {
        var packet = new AppUpdatePacket
        {
            Version = "1.1.0",
            DownloadUrl = "http://192.168.1.10:9006/student_setup.exe",
            Sha256 = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
            FileSizeBytes = 10485760
        };

        byte[] bytes = PacketSerializer.Serialize(CommandType.AppUpdate, packet);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > PacketHeader.HeaderSize);

        bool headerParsed = PacketSerializer.TryDeserializeHeader(bytes, out var header);
        Assert.True(headerParsed);
        Assert.Equal(CommandType.AppUpdate, header.Type);

        var deserialized = PacketSerializer.DeserializePayload<AppUpdatePacket>(bytes, PacketHeader.HeaderSize, header.PayloadLength);
        Assert.NotNull(deserialized);
        Assert.Equal(packet.Version, deserialized.Version);
        Assert.Equal(packet.DownloadUrl, deserialized.DownloadUrl);
        Assert.Equal(packet.Sha256, deserialized.Sha256);
        Assert.Equal(packet.FileSizeBytes, deserialized.FileSizeBytes);
    }

    [Fact]
    public async Task LanUpdateServer_ServesFileOverHttp_AndMatchesSha256()
    {
        int testPort = 19006;
        string tempFile = Path.Combine(Path.GetTempPath(), $"TestUpdate_{Guid.NewGuid():N}.exe");
        byte[] sampleBytes = new byte[1024 * 64]; // 64 KB
        new Random(42).NextBytes(sampleBytes);
        await File.WriteAllBytesAsync(tempFile, sampleBytes);

        using var server = new LanUpdateServer();
        try
        {
            server.Start(tempFile, testPort);

            string expectedSha256 = await LanUpdateServer.ComputeFileSha256Async(tempFile);
            Assert.False(string.IsNullOrEmpty(expectedSha256));

            using var httpClient = new HttpClient();
            byte[] downloadedBytes = await httpClient.GetByteArrayAsync($"http://127.0.0.1:{testPort}/setup.exe");

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
