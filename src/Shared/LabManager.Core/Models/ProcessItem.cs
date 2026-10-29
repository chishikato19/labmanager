namespace LabManager.Core.Models;

public class ProcessItem
{
    public int Pid { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string MainWindowTitle { get; set; } = string.Empty;
    public long MemoryUsageMb { get; set; }

    public bool HasWindow => !string.IsNullOrWhiteSpace(MainWindowTitle);
}
