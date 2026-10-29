namespace LabManager.Protocol.Packets;

public class WebViolationPacket
{
    public string MachineId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string BrowserName { get; set; } = string.Empty;
    public string TabTitle { get; set; } = string.Empty;
    public string BlockedKeyword { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}
