using LabManager.Core.Models;

namespace LabManager.Protocol.Packets;

public class FileCollectRequestPacket
{
    public string SourceSubFolder { get; set; } = "BaiTap";
    public string TeacherIp { get; set; } = string.Empty;
    public int TeacherFilePort { get; set; } = NetworkConfig.DefaultFilePort;

    /// <summary>
    /// true: Giáo viên đang mở đợt thu bài; false: Giáo viên đóng đợt thu bài.
    /// </summary>
    public bool IsOpen { get; set; } = true;

    /// <summary>
    /// Tiêu đề đợt thu bài hiển thị cho học sinh.
    /// </summary>
    public string SessionTitle { get; set; } = "Nộp bài cho Giáo viên";

    /// <summary>
    /// Hướng dẫn nộp bài cho học sinh.
    /// </summary>
    public string Instructions { get; set; } = "Vui lòng chọn tệp bài làm để nộp cho giáo viên.";

    /// <summary>
    /// Nếu true, tự động thu thập từ thư mục SourceSubFolder trên Desktop mà không cần học sinh thao tác thủ công.
    /// </summary>
    public bool AutoCollectDesktop { get; set; } = false;
}
