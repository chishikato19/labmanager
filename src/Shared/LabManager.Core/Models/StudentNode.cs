using LabManager.Core.Enums;

namespace LabManager.Core.Models;

public class StudentNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string MachineName { get; set; } = string.Empty;
    public string CurrentUser { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public int CommandPort { get; set; }
    public MachineStatus Status { get; set; } = MachineStatus.Online;
    public string AppVersion { get; set; } = "1.3.7";
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}
