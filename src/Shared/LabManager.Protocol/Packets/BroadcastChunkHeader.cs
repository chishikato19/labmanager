using System.Buffers.Binary;

namespace LabManager.Protocol.Packets;

public readonly struct BroadcastChunkHeader
{
    public const int HeaderSize = 12;
    public static readonly byte[] Magic = "LBCH"u8.ToArray();

    public uint FrameId { get; }
    public ushort ChunkIndex { get; }
    public ushort ChunkCount { get; }

    public BroadcastChunkHeader(uint frameId, ushort chunkIndex, ushort chunkCount)
    {
        FrameId = frameId;
        ChunkIndex = chunkIndex;
        ChunkCount = chunkCount;
    }

    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < HeaderSize)
            throw new ArgumentException("Destination too short for BroadcastChunkHeader.");

        Magic.CopyTo(destination[..4]);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), FrameId);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(8, 2), ChunkIndex);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(10, 2), ChunkCount);
    }

    public static bool TryParse(ReadOnlySpan<byte> source, out BroadcastChunkHeader header)
    {
        header = default;
        if (source.Length < HeaderSize)
            return false;

        if (!source[..4].SequenceEqual(Magic))
            return false;

        uint frameId = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(4, 4));
        ushort chunkIndex = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(8, 2));
        ushort chunkCount = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(10, 2));

        header = new BroadcastChunkHeader(frameId, chunkIndex, chunkCount);
        return true;
    }
}
