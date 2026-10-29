namespace LabManager.Core.Models;

public class ChatMessageItem
{
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsFromMe { get; set; }
    public bool IsBroadcast { get; set; }
    public bool IsAnnouncement { get; set; }
    public int DurationSeconds { get; set; }

    public string TimeFormatted => SentAt.ToLocalTime().ToString("HH:mm");
}
