namespace LabManager.Protocol.Packets;

public class ScreenFramePacket
{
    public string MachineId { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
