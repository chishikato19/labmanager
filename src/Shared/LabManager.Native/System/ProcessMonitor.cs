using System.Diagnostics;

namespace LabManager.Native.System;

public class ProcessMonitor : IDisposable
{
    private readonly HashSet<string> _blacklist = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private Timer? _scanTimer;
    private bool _isDisposed;

    public event Action<string, string>? OnViolationDetected;

    public void SetBlacklist(IEnumerable<string> processNames)
    {
        lock (_lock)
        {
            _blacklist.Clear();
            foreach (var name in processNames)
            {
                string clean = name.Trim();
                if (clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    clean = clean[..^4];
                if (!string.IsNullOrWhiteSpace(clean))
                    _blacklist.Add(clean);
            }
        }
    }

    public void ClearBlacklist()
    {
        lock (_lock) { _blacklist.Clear(); }
    }

    public void Start(int intervalMs = 2000)
    {
        _scanTimer?.Dispose();
        _scanTimer = new Timer(ScanProcesses, null, intervalMs, intervalMs);
    }

    public void Stop()
    {
        _scanTimer?.Dispose();
        _scanTimer = null;
    }

    private void ScanProcesses(object? state)
    {
        List<string> currentBlacklist;
        lock (_lock)
        {
            if (_blacklist.Count == 0) return;
            currentBlacklist = _blacklist.ToList();
        }

        foreach (var blocked in currentBlacklist)
        {
            var matched = Process.GetProcessesByName(blocked);
            foreach (var p in matched)
            {
                string winTitle = string.Empty;
                try { winTitle = p.MainWindowTitle; } catch { }

                try { p.Kill(); } catch { }
                finally { p.Dispose(); }

                OnViolationDetected?.Invoke(blocked, winTitle);
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
    }
}
