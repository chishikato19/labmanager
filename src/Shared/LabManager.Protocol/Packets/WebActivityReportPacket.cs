using LabManager.Core.Models;

namespace LabManager.Protocol.Packets;

public class WebActivityReportPacket
{
    public string MachineId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public List<WebActivityItem> Activities { get; set; } = new();
}
