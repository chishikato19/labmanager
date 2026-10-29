namespace LabManager.Protocol.Packets;

public class LockControlPacket
{
    public bool LockScreen { get; set; }
    public bool BlockInput { get; set; }
    public string Message { get; set; } = "Máy tính đang bị khóa bởi giáo viên.";
}
