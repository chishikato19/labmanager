using LabManager.Core.Enums;

namespace LabManager.Core.Interfaces;

public interface ICommandDispatcher
{
    Task DispatchAsync(CommandType type, byte[] payload, CancellationToken cancellationToken = default);
}
