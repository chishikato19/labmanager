using LabManager.Core.Enums;
using LabManager.Protocol;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;
using Xunit;

namespace LabManager.Network.Tests;

public class RemoteControlTests
{
    [Fact]
    public void RemoteInputPacket_SerializeAndDeserialize_MaintainsData()
    {
        var packet = new RemoteInputPacket
        {
            InputType = RemoteInputType.MouseDown,
            X = 0.523,
            Y = 0.812,
            Button = 1,
            WheelDelta = 120,
            VirtualKey = 0x41
        };

        byte[] bytes = PacketSerializer.Serialize(CommandType.RemoteInput, packet);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > PacketHeader.HeaderSize);

        bool parsed = PacketHeader.TryParse(bytes.AsSpan(0, PacketHeader.HeaderSize), out var header);
        Assert.True(parsed);
        Assert.Equal(CommandType.RemoteInput, header.Type);

        var deserialized = PacketSerializer.DeserializePayload<RemoteInputPacket>(bytes, PacketHeader.HeaderSize, header.PayloadLength);
        Assert.NotNull(deserialized);
        Assert.Equal(RemoteInputType.MouseDown, deserialized.InputType);
        Assert.Equal(0.523, deserialized.X, 3);
        Assert.Equal(0.812, deserialized.Y, 3);
        Assert.Equal(1, deserialized.Button);
        Assert.Equal(120, deserialized.WheelDelta);
        Assert.Equal(0x41, deserialized.VirtualKey);
    }

    [Fact]
    public void RemoteSessionPacket_SerializeAndDeserialize_MaintainsData()
    {
        var packet = new RemoteSessionPacket
        {
            Start = true,
            TeacherIp = "192.168.1.100"
        };

        byte[] bytes = PacketSerializer.Serialize(CommandType.RemoteSession, packet);
        bool parsed = PacketHeader.TryParse(bytes.AsSpan(0, PacketHeader.HeaderSize), out var header);
        Assert.True(parsed);
        Assert.Equal(CommandType.RemoteSession, header.Type);

        var deserialized = PacketSerializer.DeserializePayload<RemoteSessionPacket>(bytes, PacketHeader.HeaderSize, header.PayloadLength);
        Assert.NotNull(deserialized);
        Assert.True(deserialized.Start);
        Assert.Equal("192.168.1.100", deserialized.TeacherIp);
    }
}
