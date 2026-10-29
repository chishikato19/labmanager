using LabManager.Core.Enums;
using LabManager.Core.Interfaces;
using LabManager.Native.System;
using LabManager.Protocol.Packets;

namespace LabManager.Student.Core.Handlers;

public class WebFilterHandler : ICommandHandler<WebFilterControlPacket>, IDisposable
{
    private readonly List<string> _activeBlockedKeywords = new();
    private readonly string _machineId;
    private readonly Timer _inspectorTimer;
    private bool _isDisposed;

    public event Action<WebViolationPacket>? OnWebViolationDetected;
    public event Func<WebActivityReportPacket, Task>? OnWebActivityReady;

    public WebFilterHandler(string machineId = "")
    {
        _machineId = machineId;
        _inspectorTimer = new Timer(InspectBrowsers, null, 3000, 3000);
    }

    public async Task HandleAsync(WebFilterControlPacket command, CancellationToken cancellationToken = default)
    {
        switch (command.Action)
        {
            case WebFilterAction.SetDomainBlacklist:
                _activeBlockedKeywords.Clear();
                _activeBlockedKeywords.AddRange(command.BlacklistedDomains);
                HostsFileHelper.ApplyBlockedDomains(command.BlacklistedDomains);
                break;

            case WebFilterAction.ClearDomainBlacklist:
                _activeBlockedKeywords.Clear();
                HostsFileHelper.ClearBlockedDomains();
                break;

            case WebFilterAction.GetWebActivity:
                var tabs = BrowserInspector.GetActiveTabs();
                var report = new WebActivityReportPacket
                {
                    MachineId = _machineId,
                    MachineName = Environment.MachineName,
                    Activities = tabs
                };
                if (OnWebActivityReady != null)
                {
                    await OnWebActivityReady.Invoke(report);
                }
                break;
        }
    }

    private void InspectBrowsers(object? state)
    {
        if (_activeBlockedKeywords.Count == 0) return;

        if (BrowserInspector.CheckViolation(_activeBlockedKeywords, out var tab, out var matchedKeyword) && tab != null)
        {
            OnWebViolationDetected?.Invoke(new WebViolationPacket
            {
                MachineId = _machineId,
                MachineName = Environment.MachineName,
                BrowserName = tab.BrowserName,
                TabTitle = tab.TabTitle,
                BlockedKeyword = matchedKeyword ?? string.Empty,
                DetectedAt = DateTime.UtcNow
            });
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _inspectorTimer.Dispose();
        HostsFileHelper.ClearBlockedDomains();
    }
}
