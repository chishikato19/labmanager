using LabManager.Core.Interfaces;
using LabManager.Protocol.Packets;

namespace LabManager.Student.Core.Handlers;

public class ChatHandler : ICommandHandler<ChatMessagePacket>
{
    public event Action<ChatMessagePacket>? OnMessageReceived;

    public Task HandleAsync(ChatMessagePacket command, CancellationToken cancellationToken = default)
    {
        OnMessageReceived?.Invoke(command);
        return Task.CompletedTask;
    }
}
