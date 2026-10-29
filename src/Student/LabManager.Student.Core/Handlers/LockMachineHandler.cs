using LabManager.Core.Interfaces;
using LabManager.Native.Input;
using LabManager.Native.System;
using LabManager.Protocol.Packets;

namespace LabManager.Student.Core.Handlers;

public class LockMachineHandler : ICommandHandler<LockControlPacket>, IDisposable
{
    private readonly KeyboardHook _keyboardHook;
    private readonly MouseHook _mouseHook;
    private bool _isDisposed;

    public bool IsLocked { get; private set; }
    public event Action<bool, string>? OnLockStateChanged;

    public LockMachineHandler()
    {
        _keyboardHook = new KeyboardHook();
        _mouseHook = new MouseHook();
    }

    public Task HandleAsync(LockControlPacket command, CancellationToken cancellationToken = default)
    {
        IsLocked = command.LockScreen;

        if (command.BlockInput)
        {
            _keyboardHook.Install();
            _mouseHook.Install();
            TaskMgrPolicy.SetTaskMgrDisabled(true);
        }
        else
        {
            _keyboardHook.Uninstall();
            _mouseHook.Uninstall();
            TaskMgrPolicy.SetTaskMgrDisabled(false);
        }

        OnLockStateChanged?.Invoke(command.LockScreen, command.Message);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _keyboardHook.Dispose();
        _mouseHook.Dispose();
        TaskMgrPolicy.SetTaskMgrDisabled(false);
    }
}
