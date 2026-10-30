using LabManager.Core.Enums;
using LabManager.Protocol;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;
using LabManager.Student.Core.Handlers;
using Xunit;

namespace LabManager.Network.Tests;

public class CleanAndInitDriveTests
{
    [Fact]
    public void CleanAndInitDrivePacket_SerializesAndDeserializes_Correctly()
    {
        var packet = new CleanAndInitDrivePacket
        {
            DriveLetter = @"D:\",
            FolderNames = new List<string> { "10A1", "10A2", "11A1", "12A3" }
        };

        byte[] bytes = PacketSerializer.Serialize(CommandType.CleanAndInitDrive, packet);

        Assert.True(PacketSerializer.TryDeserializeHeader(bytes, out var header));
        Assert.Equal(CommandType.CleanAndInitDrive, header.Type);

        var deserialized = PacketSerializer.DeserializePayload<CleanAndInitDrivePacket>(bytes, PacketHeader.HeaderSize, header.PayloadLength);
        Assert.NotNull(deserialized);
        Assert.Equal(@"D:\", deserialized.DriveLetter);
        Assert.Equal(4, deserialized.FolderNames.Count);
        Assert.Contains("10A1", deserialized.FolderNames);
        Assert.Contains("12A3", deserialized.FolderNames);
    }

    [Fact]
    public async Task CleanAndInitDriveHandler_Rejects_SystemDriveC()
    {
        var handler = new CleanAndInitDriveHandler();
        var packet = new CleanAndInitDrivePacket
        {
            DriveLetter = @"C:\",
            FolderNames = new List<string> { "TestFolder" }
        };

        bool result = await handler.HandleAsync(packet);

        // Bắt buộc phải từ chối khi chỉ định ổ C:\
        Assert.False(result);
    }

    [Fact]
    public async Task CleanAndInitDriveHandler_Rejects_NonExistentDrive()
    {
        var handler = new CleanAndInitDriveHandler();
        var packet = new CleanAndInitDrivePacket
        {
            DriveLetter = @"Z:\NonExistentDriveXYZ\",
            FolderNames = new List<string> { "TestFolder" }
        };

        bool result = await handler.HandleAsync(packet);
        Assert.False(result);
    }
}
