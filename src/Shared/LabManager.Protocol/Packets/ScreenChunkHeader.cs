using System.Buffers.Binary;

namespace LabManager.Protocol.Packets;

public readonly struct ScreenChunkHeader
{
    public const int HeaderSize = 32;
    public static readonly byte[] Magic = "LMSC"u8.ToArray(); // LabManager Screen Chunk

    public Guid MachineId { get; }
    public uint FrameId { get; }
    public ushort ChunkIndex { get; }
    public ushort ChunkCount { get; }
    public ushort Width { get; }
    public ushort Height { get; }

    public ScreenChunkHeader(Guid machineId, uint frameId, ushort chunkIndex, ushort chunkCount, ushort width, ushort height)
    {
        MachineId = machineId;
        FrameId = frameId;
        ChunkIndex = chunkIndex;
        ChunkCount = chunkCount;
        Width = width;
        Height = height;
    }

    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < HeaderSize)
            throw new ArgumentException("Destination too short for ScreenChunkHeader.");

        Magic.CopyTo(destination[..4]);
        MachineId.TryWriteBytes(destination.Slice(4, 16));
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(20, 4), FrameId);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(24, 2), ChunkIndex);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(26, 2), ChunkCount);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(28, 2), Width);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(30, 2), Height);
    }

    public static bool TryParse(ReadOnlySpan<byte> source, out ScreenChunkHeader header)
    {
        header = default;
        if (source.Length < HeaderSize)
            return false;

        if (!source[..4].SequenceEqual(Magic))
            return false;

        var machineId = new Guid(source.Slice(4, 16));
        uint frameId = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(20, 4));
        ushort chunkIndex = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(24, 2));
        ushort chunkCount = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(26, 2));
        ushort width = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(28, 2));
        ushort height = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(30, 2));

        header = new ScreenChunkHeader(machineId, frameId, chunkIndex, chunkCount, width, height);
        return true;
    }
}
