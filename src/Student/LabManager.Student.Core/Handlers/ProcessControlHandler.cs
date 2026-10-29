using LabManager.Core.Enums;
using LabManager.Core.Interfaces;
using LabManager.Native.System;
using LabManager.Protocol.Packets;

namespace LabManager.Student.Core.Handlers;

public class ProcessControlHandler : ICommandHandler<ProcessControlPacket>, IDisposable
{
    private readonly ProcessMonitor _monitor = new();
    private readonly string _machineId;
    private bool _isDisposed;

    public ProcessMonitor Monitor => _monitor;
    public event Action<ProcessViolationPacket>? OnViolationDetected;
    public event Func<ProcessListResponsePacket, Task>? OnProcessListReady;

    public ProcessControlHandler(string machineId = "")
    {
        _machineId = machineId;
        _monitor.OnViolationDetected += (procName, winTitle) =>
        {
            OnViolationDetected?.Invoke(new ProcessViolationPacket
            {
                MachineId = _machineId,
                MachineName = Environment.MachineName,
                ProcessName = procName,
                WindowTitle = winTitle,
                DetectedAt = DateTime.UtcNow
            });
        };
        _monitor.Start(intervalMs: 2000);
    }

    public async Task HandleAsync(ProcessControlPacket command, CancellationToken cancellationToken = default)
    {
        switch (command.Action)
        {
            case ProcessAction.SetBlacklist:
                _monitor.SetBlacklist(command.Blacklist);
                break;

            case ProcessAction.ClearBlacklist:
                _monitor.ClearBlacklist();
                break;

            case ProcessAction.KillProcess:
                if (command.TargetPid > 0)
                {
                    ProcessHelper.KillProcessByPid(command.TargetPid);
                }
                else if (!string.IsNullOrWhiteSpace(command.TargetProcessName))
                {
                    ProcessHelper.KillProcessesByName(command.TargetProcessName);
                }
                break;

            case ProcessAction.GetRunningProcesses:
                var list = ProcessHelper.GetActiveProcesses(windowedOnly: false);
                var response = new ProcessListResponsePacket
                {
                    MachineId = _machineId,
                    MachineName = Environment.MachineName,
                    Processes = list
                };
                if (OnProcessListReady != null)
                {
                    await OnProcessListReady.Invoke(response);
                }
                break;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _monitor.Dispose();
    }
}
