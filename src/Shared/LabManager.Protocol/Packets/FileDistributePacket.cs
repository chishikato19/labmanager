namespace LabManager.Protocol.Packets;

public class FileDistributePacket
{
    public string FileName { get; set; } = string.Empty;
    public string TargetSubFolder { get; set; } = "BaiTap";
    public byte[] FileBytes { get; set; } = Array.Empty<byte>();
    public bool AutoExtract { get; set; } = true;
}
