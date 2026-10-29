namespace LabManager.Protocol.Packets;

public enum RemoteInputType : byte
{
    MouseMove = 1,
    MouseDown = 2,
    MouseUp = 3,
    MouseWheel = 4,
    KeyDown = 5,
    KeyUp = 6
}

public class RemoteInputPacket
{
    public RemoteInputType InputType { get; set; }
    public double X { get; set; } // Tỷ lệ tọa độ ngang (0.0 -> 1.0)
    public double Y { get; set; } // Tỷ lệ tọa độ dọc (0.0 -> 1.0)
    public int Button { get; set; } // 0: Trái, 1: Phải, 2: Giữa
    public int WheelDelta { get; set; } // Giá trị lăn chuột (+/- 120)
    public int VirtualKey { get; set; } // Mã phím ảo Windows
}
