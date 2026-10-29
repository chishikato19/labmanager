using LabManager.Core.Interfaces;
using LabManager.Native.System;
using LabManager.Protocol.Packets;

namespace LabManager.Student.Core.Handlers;

public class PowerControlHandler : ICommandHandler<PowerControlPacket>
{
    public Action<PowerControlPacket>? CustomExecutor { get; set; }
    public event Action<PowerControlPacket>? OnPowerActionExecuting;

    public Task HandleAsync(PowerControlPacket command, CancellationToken cancellationToken = default)
    {
        OnPowerActionExecuting?.Invoke(command);

        if (CustomExecutor != null)
        {
            CustomExecutor(command);
        }
        else
        {
            PowerController.ExecuteCommand(command.Action, command.Force, command.DelaySeconds);
        }

        return Task.CompletedTask;
    }
}
