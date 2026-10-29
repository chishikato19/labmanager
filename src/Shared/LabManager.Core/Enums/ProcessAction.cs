namespace LabManager.Core.Enums;

public enum ProcessAction : byte
{
    GetRunningProcesses = 0,
    KillProcess = 1,
    SetBlacklist = 2,
    ClearBlacklist = 3
}
