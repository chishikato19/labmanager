namespace LabManager.Core.Models;

public class WebActivityItem
{
    public string BrowserName { get; set; } = string.Empty;
    public string TabTitle { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}
