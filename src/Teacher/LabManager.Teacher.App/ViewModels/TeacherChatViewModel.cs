using System.Collections.ObjectModel;
using LabManager.Core.Models;
using LabManager.Protocol.Packets;
using LabManager.Teacher.Core.Services;

namespace LabManager.Teacher.App.ViewModels;

public class TeacherChatViewModel : ViewModelBase
{
    private readonly ClassroomManager _manager;
    private readonly string _targetIp;
    private readonly string _targetId;
    private readonly string _targetName;
    private readonly bool _isBroadcast;
    private string _inputText = string.Empty;

    private bool _isAnnouncement;
    private int _durationSeconds = 10;
    private bool _allowCloseChat = true;
    private bool _allowStudentReply = true;

    public string Title => _isBroadcast ? "📢 Tin nhắn & Thông báo toàn phòng" : $"💬 Trao đổi với: {_targetName}";
    public ObservableCollection<ChatMessageItem> Messages { get; } = new();

    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    public bool IsAnnouncement
    {
        get => _isAnnouncement;
        set => SetProperty(ref _isAnnouncement, value);
    }

    public int DurationSeconds
    {
        get => _durationSeconds;
        set => SetProperty(ref _durationSeconds, value);
    }

    public bool AllowCloseChat
    {
        get => _allowCloseChat;
        set => SetProperty(ref _allowCloseChat, value);
    }

    public bool AllowStudentReply
    {
        get => _allowStudentReply;
        set => SetProperty(ref _allowStudentReply, value);
    }

    public ObservableCollection<int> DurationOptions { get; } = new() { 5, 10, 15, 20, 30, 60 };

    public RelayCommand SendCommand { get; }
    public RelayCommand CloseStudentChatCommand { get; }

    public TeacherChatViewModel(ClassroomManager manager, string targetIp, string targetId, string targetName, bool isBroadcast = false)
    {
        _manager = manager;
        _targetIp = targetIp;
        _targetId = targetId;
        _targetName = targetName;
        _isBroadcast = isBroadcast;

        SendCommand = new RelayCommand(async () => await SendAsync());
        CloseStudentChatCommand = new RelayCommand(async () => await CloseStudentChatAsync());
    }

    public void ReceiveMessage(ChatMessagePacket packet)
    {
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

    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        string content = InputText.Trim();
        InputText = string.Empty;

        string senderTitle = _isAnnouncement
            ? (_isBroadcast ? "Tôi (Thông báo toàn phòng)" : "Tôi (Thông báo nổi)")
            : "Tôi (Giáo viên)";

        Messages.Add(new ChatMessageItem
        {
            SenderName = senderTitle,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsFromMe = true,
            IsBroadcast = _isBroadcast,
            IsAnnouncement = _isAnnouncement,
            DurationSeconds = _durationSeconds
        });

        if (_isBroadcast)
        {
            await _manager.SendBroadcastMessageAsync(
                content,
                isAnnouncement: _isAnnouncement,
                durationSeconds: _durationSeconds,
                allowCloseChat: _allowCloseChat,
                allowStudentReply: _allowStudentReply);
        }
        else
        {
            await _manager.SendChatMessageAsync(
                _targetIp,
                content,
                _targetId,
                _targetName,
                isAnnouncement: _isAnnouncement,
                durationSeconds: _durationSeconds,
                allowCloseChat: _allowCloseChat,
                allowStudentReply: _allowStudentReply);
        }
    }

    private async Task CloseStudentChatAsync()
    {
        if (_isBroadcast)
        {
            await _manager.CloseAllStudentsChatAsync();
        }
        else
        {
            await _manager.CloseStudentChatAsync(_targetIp);
        }

        Messages.Add(new ChatMessageItem
        {
            SenderName = "Hệ thống",
            Content = "🚫 Đã gửi lệnh tắt bảng chat phía học sinh.",
            SentAt = DateTime.UtcNow,
            IsFromMe = true
        });
    }
}
