using LabManager.Core.Enums;
using LabManager.Native.System;
using LabManager.Protocol.Packets;

namespace LabManager.Student.Core.Handlers;

public class DriveSecurityHandler
{
    public Task<bool> HandleAsync(DriveSecurityPacket packet)
    {
        string drive = string.IsNullOrWhiteSpace(packet.DriveLetter) ? @"D:\" : packet.DriveLetter;

        bool success;
        if (packet.Action == DriveSecurityAction.LockDrive)
        {
            success = DriveSecurityHelper.LockDrive(drive);
        }
        else
        {
            success = DriveSecurityHelper.UnlockDrive(drive);
        }

        return Task.FromResult(success);
    }
}
