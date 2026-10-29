namespace LabManager.Protocol.Packets;

public class DiscoveryBeaconPacket
{
    public string TeacherName { get; set; } = string.Empty;
    public string RoomName { get; set; } = "Phòng máy";
    public int TeacherPort { get; set; }
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
