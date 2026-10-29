using System.Text.Json;
using LabManager.Core.Enums;

namespace LabManager.Protocol.Serializer;

public static class PacketSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static byte[] Serialize<T>(CommandType type, T payload)
    {
        byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        byte[] result = new byte[PacketHeader.HeaderSize + payloadBytes.Length];

        var header = new PacketHeader(type, payloadBytes.Length);
        header.WriteTo(result.AsSpan(0, PacketHeader.HeaderSize));
        Buffer.BlockCopy(payloadBytes, 0, result, PacketHeader.HeaderSize, payloadBytes.Length);

        return result;
    }

    public static bool TryDeserializeHeader(byte[] buffer, out PacketHeader header)
    {
        return PacketHeader.TryParse(buffer, out header);
    }

    public static T? DeserializePayload<T>(ReadOnlySpan<byte> payloadSpan)
    {
        return JsonSerializer.Deserialize<T>(payloadSpan, JsonOptions);
    }

    public static T? DeserializePayload<T>(byte[] buffer, int offset, int length)
    {
        var span = new ReadOnlySpan<byte>(buffer, offset, length);
        return JsonSerializer.Deserialize<T>(span, JsonOptions);
    }
}
