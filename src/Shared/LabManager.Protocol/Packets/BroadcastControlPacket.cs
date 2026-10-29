using LabManager.Core.Models;

namespace LabManager.Protocol.Packets;

public class BroadcastControlPacket
{
    public bool IsBroadcasting { get; set; }
    public string MulticastGroup { get; set; } = NetworkConfig.DefaultMulticastGroup;
    public int MulticastPort { get; set; } = NetworkConfig.DefaultMulticastPort;
    public bool Fullscreen { get; set; } = true;
    public string Message { get; set; } = "Giáo viên đang chiếu màn hình bài giảng.";
    public string PresenterName { get; set; } = string.Empty;
    public string ExcludedMachineId { get; set; } = string.Empty;
}
