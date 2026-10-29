using LabManager.Core.Enums;

namespace LabManager.Protocol.Packets;

public class DiscoveryResponsePacket
{
    public string MachineId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string CurrentUser { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public int CommandPort { get; set; }
    public MachineStatus Status { get; set; } = MachineStatus.Online;
    public string OsVersion { get; set; } = string.Empty;
    public string AppVersion { get; set; } = "1.3.2";
}
