namespace LabManager.Protocol.Packets;

public class LaunchAppPacket
{
    public string AppNameOrPath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
}
