using LabManager.Core.Models;

namespace LabManager.Protocol.Packets;

public class ProcessListResponsePacket
{
    public string MachineId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public List<ProcessItem> Processes { get; set; } = new();
}
