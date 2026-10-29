using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LabManager.Core.Models;
using LabManager.Protocol.Packets;
using LabManager.Student.Core.Services;

namespace LabManager.Student.App.ViewModels;

public class ChatViewModel : INotifyPropertyChanged
{
    private readonly StudentCoordinator _coordinator;
    private string _inputText = string.Empty;
    private bool _allowCloseChat = true;
    private bool _allowStudentReply = true;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? OnRequestClose;

    public ObservableCollection<ChatMessageItem> Messages { get; } = new();

    public string InputText
    {
        get => _inputText;
        set { _inputText = value; OnPropertyChanged(); }
    }

    public bool AllowCloseChat
    {
        get => _allowCloseChat;
        set { _allowCloseChat = value; OnPropertyChanged(); }
    }

    public bool AllowStudentReply
    {
        get => _allowStudentReply;
        set { _allowStudentReply = value; OnPropertyChanged(); }
    }

    public StudentRelayCommand SendCommand { get; }
    public StudentRelayCommand RequestCloseCommand { get; }

    public ChatViewModel(StudentCoordinator coordinator)
    {
        _coordinator = coordinator;
        SendCommand = new StudentRelayCommand(async () => await SendMessageAsync());
        RequestCloseCommand = new StudentRelayCommand(() =>
        {
            if (AllowCloseChat)
            {
                OnRequestClose?.Invoke();
            }
        });
    }

    public void ReceiveMessage(ChatMessagePacket packet)
    {
        AllowCloseChat = packet.AllowCloseChat;
        AllowStudentReply = packet.AllowStudentReply;

        Messages.Add(new ChatMessageItem
        {
            SenderName = packet.SenderName,
            Content = packet.Content,
            SentAt = packet.SentAt,
            IsFromMe = false,
            IsBroadcast = packet.IsBroadcast,
            IsAnnouncement = packet.IsAnnouncement,
            DurationSeconds = packet.DurationSeconds
        });
    }

    private async Task SendMessageAsync()
    {
        if (!AllowStudentReply || string.IsNullOrWhiteSpace(InputText)) return;

        string content = InputText.Trim();
        InputText = string.Empty;

        Messages.Add(new ChatMessageItem
        {
            SenderName = "Tôi",
            Content = content,
            SentAt = DateTime.UtcNow,
            IsFromMe = true,
            IsBroadcast = false
        });

        await _coordinator.SendChatMessageAsync(content);
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
