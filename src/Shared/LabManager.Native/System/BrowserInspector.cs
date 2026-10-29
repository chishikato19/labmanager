using System.Diagnostics;
using LabManager.Core.Models;

namespace LabManager.Native.System;

public static class BrowserInspector
{
    private static readonly string[] BrowserProcessNames =
    [
        "chrome", "msedge", "firefox", "brave", "opera", "coccoc"
    ];

    public static List<WebActivityItem> GetActiveTabs()
    {
        var result = new List<WebActivityItem>();

        foreach (var browserName in BrowserProcessNames)
        {
            var processes = Process.GetProcessesByName(browserName);
            foreach (var p in processes)
            {
                try
                {
                    if (p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                    {
                        result.Add(new WebActivityItem
                        {
                            BrowserName = browserName,
                            TabTitle = p.MainWindowTitle,
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                }
                catch { }
                finally { p.Dispose(); }
            }
        }

        return result;
    }

    public static bool CheckViolation(IEnumerable<string> blockedKeywords, out WebActivityItem? violation, out string? matchedKeyword)
    {
        violation = null;
        matchedKeyword = null;

        var tabs = GetActiveTabs();
        var keywords = blockedKeywords.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();

        foreach (var tab in tabs)
        {
            foreach (var kw in keywords)
            {
                if (tab.TabTitle.Contains(kw, StringComparison.OrdinalIgnoreCase))
                {
                    violation = tab;
                    matchedKeyword = kw;
                    return true;
                }
            }
        }

        return false;
    }
}
