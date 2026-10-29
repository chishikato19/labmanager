using LabManager.Core.Enums;

namespace LabManager.Protocol.Packets;

public class PowerControlPacket
{
    public PowerAction Action { get; set; } = PowerAction.Shutdown;
    public bool Force { get; set; } = true;
    public int DelaySeconds { get; set; } = 0;
    public string Message { get; set; } = string.Empty;
}
