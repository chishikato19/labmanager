namespace LabManager.Protocol.Packets;

public class CleanAndInitDrivePacket
{
    public string DriveLetter { get; set; } = @"D:\";
    public List<string> FolderNames { get; set; } = new();
}
