namespace LabManager.Core.Enums;

public enum WebFilterAction : byte
{
    SetDomainBlacklist = 0,
    ClearDomainBlacklist = 1,
    BlockAllWeb = 2,
    UnblockAllWeb = 3,
    GetWebActivity = 4
}
