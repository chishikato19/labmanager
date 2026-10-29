namespace LabManager.Protocol.Packets;

public class OpenWebUrlPacket
{
    public string Url { get; set; } = string.Empty;
    public bool ForceChrome { get; set; } = true;
}
