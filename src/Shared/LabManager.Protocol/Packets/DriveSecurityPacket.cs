using LabManager.Core.Enums;

namespace LabManager.Protocol.Packets;

public class DriveSecurityPacket
{
    public DriveSecurityAction Action { get; set; } = DriveSecurityAction.LockDrive;
    public string DriveLetter { get; set; } = @"D:\";
}
