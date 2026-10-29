using LabManager.Core.Enums;

namespace LabManager.Protocol.Packets;

public class WebFilterControlPacket
{
    public WebFilterAction Action { get; set; } = WebFilterAction.SetDomainBlacklist;
    public List<string> BlacklistedDomains { get; set; } = new();
    public bool BlockAllBrowsing { get; set; }
}
