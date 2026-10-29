using System.Diagnostics;
using LabManager.Core.Models;

namespace LabManager.Native.System;

public static class ProcessHelper
{
    public static List<ProcessItem> GetActiveProcesses(bool windowedOnly = true)
    {
        var result = new List<ProcessItem>();
        var processes = Process.GetProcesses();

        foreach (var p in processes)
        {
            try
            {
                string title = p.MainWindowTitle;
                bool hasWindow = p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(title);

                if (windowedOnly && !hasWindow)
                {
                    continue;
                }

                long memoryMb = 0;
                try { memoryMb = p.WorkingSet64 / (1024 * 1024); } catch { }

                result.Add(new ProcessItem
                {
                    Pid = p.Id,
                    ProcessName = p.ProcessName,
                    MainWindowTitle = title,
                    MemoryUsageMb = memoryMb
                });
            }
            catch
            {
                // Bỏ qua các tiến trình hệ thống bị giới hạn quyền truy cập
            }
            finally
            {
                p.Dispose();
            }
        }

        return result.OrderBy(p => p.ProcessName).ToList();
    }

    public static bool KillProcessByPid(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static int KillProcessesByName(string processName)
    {
        int killedCount = 0;
        string cleanName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        var processes = Process.GetProcessesByName(cleanName);
        foreach (var p in processes)
        {
            try
            {
                p.Kill();
                killedCount++;
            }
            catch { }
            finally { p.Dispose(); }
        }

        return killedCount;
    }
}
