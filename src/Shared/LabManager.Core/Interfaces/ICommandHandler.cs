namespace LabManager.Core.Interfaces;

public interface ICommandHandler<in TCommand> where TCommand : class
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
