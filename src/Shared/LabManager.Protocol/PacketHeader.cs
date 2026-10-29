using System.Buffers.Binary;
using LabManager.Core.Enums;

namespace LabManager.Protocol;

public readonly struct PacketHeader
{
    public const int HeaderSize = 12;
    public static readonly byte[] MagicBytes = "LABM"u8.ToArray();

    public ushort Version { get; }
    public CommandType Type { get; }
    public int PayloadLength { get; }

    public PacketHeader(CommandType type, int payloadLength, ushort version = 1)
    {
        Version = version;
        Type = type;
        PayloadLength = payloadLength;
    }

    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < HeaderSize)
            throw new ArgumentException("Destination span is too short for packet header.");

        MagicBytes.CopyTo(destination[..4]);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), Version);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(6, 2), (ushort)Type);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(8, 4), PayloadLength);
    }

    public static bool TryParse(ReadOnlySpan<byte> source, out PacketHeader header)
    {
        header = default;
        if (source.Length < HeaderSize)
            return false;

        if (!source[..4].SequenceEqual(MagicBytes))
            return false;

        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(4, 2));
        CommandType type = (CommandType)BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(6, 2));
        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(8, 4));

        header = new PacketHeader(type, payloadLength, version);
        return true;
    }
}
