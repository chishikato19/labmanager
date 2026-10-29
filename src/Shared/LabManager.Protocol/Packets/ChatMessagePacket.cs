namespace LabManager.Protocol.Packets;

public class ChatMessagePacket
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsBroadcast { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    // Tính năng thông báo & Tùy chọn kiểm soát bảng chat
    public bool IsAnnouncement { get; set; }
    public int DurationSeconds { get; set; } = 10;
    public bool AllowCloseChat { get; set; } = true;
    public bool AllowStudentReply { get; set; } = true;
    public bool CloseChatCommand { get; set; }
}
