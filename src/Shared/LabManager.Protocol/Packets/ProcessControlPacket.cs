using LabManager.Core.Enums;

namespace LabManager.Protocol.Packets;

public class ProcessControlPacket
{
    public ProcessAction Action { get; set; } = ProcessAction.GetRunningProcesses;
    public int TargetPid { get; set; }
    public string TargetProcessName { get; set; } = string.Empty;
    public List<string> Blacklist { get; set; } = new();
}
