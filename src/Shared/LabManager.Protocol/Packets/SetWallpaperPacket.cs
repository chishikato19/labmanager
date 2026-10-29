namespace LabManager.Protocol.Packets;

public class SetWallpaperPacket
{
    public string FileName { get; set; } = string.Empty;
    public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
    
    /// <summary>
    /// Kiểu hiển thị hình nền: 10 (Fill - lấp đầy), 2 (Fit - vừa vặn), 6 (Stretch - kéo dãn), 0 (Center - căn giữa)
    /// </summary>
    public int Style { get; set; } = 10;
}
