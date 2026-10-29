namespace LabManager.Protocol.Packets;

public class FileCollectResponsePacket
{
    public string MachineId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string ZipFileName { get; set; } = string.Empty;
    public byte[] ZipBytes { get; set; } = Array.Empty<byte>();
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string Note { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}
