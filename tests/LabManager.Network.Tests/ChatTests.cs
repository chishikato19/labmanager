using LabManager.Core.Enums;
using LabManager.Network.Tcp;
using LabManager.Protocol;
using LabManager.Protocol.Packets;
using LabManager.Protocol.Serializer;
using LabManager.Student.Core.Handlers;

namespace LabManager.Network.Tests;

public class ChatTests
{
    [Fact]
    public void ChatMessagePacket_Serialization_PreservesAllFields()
    {
        var packet = new ChatMessagePacket
        {
            SenderId = "TEACHER_1",
            SenderName = "Thầy Hùng",
            TargetId = "STUDENT_5",
            TargetName = "Máy 05",
            Content = "Em đã làm xong bài tập số 3 chưa?",
            IsBroadcast = false,
            IsAnnouncement = true,
            DurationSeconds = 15,
            AllowCloseChat = false,
            AllowStudentReply = false,
            CloseChatCommand = false
        };

        byte[] bytes = PacketSerializer.Serialize(CommandType.ChatMessage, packet);
        Assert.True(PacketSerializer.TryDeserializeHeader(bytes, out var header));
        Assert.Equal(CommandType.ChatMessage, header.Type);

        var deserialized = PacketSerializer.DeserializePayload<ChatMessagePacket>(bytes, PacketHeader.HeaderSize, header.PayloadLength);
        Assert.NotNull(deserialized);
        Assert.Equal("TEACHER_1", deserialized.SenderId);
        Assert.Equal("Thầy Hùng", deserialized.SenderName);
        Assert.Equal("Máy 05", deserialized.TargetName);
        Assert.Equal("Em đã làm xong bài tập số 3 chưa?", deserialized.Content);
        Assert.False(deserialized.IsBroadcast);
        Assert.True(deserialized.IsAnnouncement);
        Assert.Equal(15, deserialized.DurationSeconds);
        Assert.False(deserialized.AllowCloseChat);
        Assert.False(deserialized.AllowStudentReply);
        Assert.False(deserialized.CloseChatCommand);
    }

    [Fact]
    public async Task TeacherAndStudent_CanExchangeChatMessages_OverTcp()
    {
        const int studentCmdPort = 9208;
        using var studentListener = new TcpCommandListener();
        var chatHandler = new ChatHandler();
        var teacherSender = new TeacherCommandSender();

        var tcsStudentReceived = new TaskCompletionSource<ChatMessagePacket>();
        chatHandler.OnMessageReceived += msg => tcsStudentReceived.TrySetResult(msg);

        studentListener.OnCommandReceived += async (header, payload) =>
        {
            if (header.Type == CommandType.ChatMessage)
            {
                var msg = PacketSerializer.DeserializePayload<ChatMessagePacket>(payload, 0, payload.Length);
                if (msg != null)
                {
                    await chatHandler.HandleAsync(msg);
                }
            }
        };

        studentListener.Start(studentCmdPort);

        // 1. Giáo viên gửi tin nhắn xuống học sinh
        var teacherMsg = new ChatMessagePacket
        {
            SenderId = "TEACHER",
            SenderName = "Giáo viên",
            Content = "Thông báo: Cả lớp chú ý nhìn lên màn hình!",
            IsBroadcast = true
        };
        byte[] bytes = PacketSerializer.Serialize(CommandType.ChatMessage, teacherMsg);

        bool sent = await teacherSender.SendCommandAsync("127.0.0.1", bytes, studentCmdPort);
        Assert.True(sent);

        var completed = await Task.WhenAny(tcsStudentReceived.Task, Task.Delay(3000));
        Assert.Same(tcsStudentReceived.Task, completed);

        var received = await tcsStudentReceived.Task;
        Assert.Equal("Giáo viên", received.SenderName);
        Assert.Equal("Thông báo: Cả lớp chú ý nhìn lên màn hình!", received.Content);
        Assert.True(received.IsBroadcast);
    }

    [Fact]
    public async Task TeacherAndStudent_AnnouncementAndChatControl_OverTcp()
    {
        const int studentCmdPort = 9218;
        using var studentListener = new TcpCommandListener();
        var chatHandler = new ChatHandler();
        var teacherSender = new TeacherCommandSender();

        var tcsAnnouncementReceived = new TaskCompletionSource<ChatMessagePacket>();
        var tcsCloseChatReceived = new TaskCompletionSource<ChatMessagePacket>();

        chatHandler.OnMessageReceived += msg =>
        {
            if (msg.CloseChatCommand)
            {
                tcsCloseChatReceived.TrySetResult(msg);
            }
            else if (msg.IsAnnouncement)
            {
                tcsAnnouncementReceived.TrySetResult(msg);
            }
        };

        studentListener.OnCommandReceived += async (header, payload) =>
        {
            if (header.Type == CommandType.ChatMessage)
            {
                var msg = PacketSerializer.DeserializePayload<ChatMessagePacket>(payload, 0, payload.Length);
                if (msg != null)
                {
                    await chatHandler.HandleAsync(msg);
                }
            }
        };

        studentListener.Start(studentCmdPort);

        // 1. Giáo viên gửi thông báo nổi tồn tại trong 20 giây, không cho tắt, không cho reply
        var announcementMsg = new ChatMessagePacket
        {
            SenderId = "TEACHER",
            SenderName = "Giáo viên (Thông báo)",
            Content = "Các em lưu bài và nộp trước khi hết giờ!",
            IsBroadcast = true,
            IsAnnouncement = true,
            DurationSeconds = 20,
            AllowCloseChat = false,
            AllowStudentReply = false
        };
        byte[] bytes1 = PacketSerializer.Serialize(CommandType.ChatMessage, announcementMsg);
        bool sent1 = await teacherSender.SendCommandAsync("127.0.0.1", bytes1, studentCmdPort);
        Assert.True(sent1);

        var completed1 = await Task.WhenAny(tcsAnnouncementReceived.Task, Task.Delay(3000));
        Assert.Same(tcsAnnouncementReceived.Task, completed1);
        var receivedAnnouncement = await tcsAnnouncementReceived.Task;
        Assert.True(receivedAnnouncement.IsAnnouncement);
        Assert.Equal(20, receivedAnnouncement.DurationSeconds);
        Assert.False(receivedAnnouncement.AllowCloseChat);
        Assert.False(receivedAnnouncement.AllowStudentReply);

        // 2. Giáo viên gửi lệnh đóng bảng chat của học sinh
        var closeCmd = new ChatMessagePacket
        {
            SenderId = "TEACHER",
            CloseChatCommand = true
        };
        byte[] bytes2 = PacketSerializer.Serialize(CommandType.ChatMessage, closeCmd);
        bool sent2 = await teacherSender.SendCommandAsync("127.0.0.1", bytes2, studentCmdPort);
        Assert.True(sent2);

        var completed2 = await Task.WhenAny(tcsCloseChatReceived.Task, Task.Delay(3000));
        Assert.Same(tcsCloseChatReceived.Task, completed2);
        var receivedCloseCmd = await tcsCloseChatReceived.Task;
        Assert.True(receivedCloseCmd.CloseChatCommand);
    }
}
