namespace LabManager.Protocol.Packets;

public class DeploySoftwareStatusPacket
{
    public string MachineId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty; // "Downloading", "Installing", "Completed", "Failed"
    public int ProgressPercentage { get; set; }
    public int ExitCode { get; set; }
    public string Message { get; set; } = string.Empty;
}
