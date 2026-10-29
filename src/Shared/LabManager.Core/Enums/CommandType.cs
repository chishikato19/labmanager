namespace LabManager.Core.Enums;

public enum CommandType : ushort
{
    // Hệ thống & Khám phá
    DiscoveryBeacon = 100,
    DiscoveryResponse = 101,
    Heartbeat = 102,

    // Giám sát màn hình
    ScreenFrame = 200,
    BroadcastStream = 201,
    BroadcastControl = 202,

    // Kiểm soát thiết bị & Nguồn
    LockControl = 300,
    PowerControl = 301,
    RemoteInput = 310,
    RemoteSession = 311,
    SetWallpaper = 320,

    // Tệp tin & Tương tác
    FileDistribute = 400,
    FileCollectRequest = 401,
    FileCollectResponse = 402,
    ChatMessage = 410,
    DeploySoftware = 420,
    DeploySoftwareStatus = 421,
    DriveSecurityControl = 430,

    // Giám sát hoạt động
    ProcessControl = 500,
    WebFilterControl = 501,
    ProcessListResponse = 502,
    ProcessViolation = 503,
    WebViolation = 504,
    WebActivityReport = 505,
    OpenWebUrl = 510,
    LaunchApp = 511,

    // Tự động cập nhật qua mạng LAN
    AppUpdate = 600
}
