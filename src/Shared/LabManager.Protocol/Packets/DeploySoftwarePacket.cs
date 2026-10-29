namespace LabManager.Protocol.Packets;

public class DeploySoftwarePacket
{
    public string DownloadUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public bool IsMsi { get; set; }
}
