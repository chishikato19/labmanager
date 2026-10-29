namespace LabManager.Protocol.Packets;

public class RemoteSessionPacket
{
    public bool Start { get; set; }
    public string TeacherIp { get; set; } = string.Empty;
    public bool HighQualityFocus { get; set; }
    public bool IsControlSession { get; set; }
}
