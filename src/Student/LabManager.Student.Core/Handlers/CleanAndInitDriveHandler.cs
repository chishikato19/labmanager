using System.Diagnostics;
using LabManager.Protocol.Packets;

namespace LabManager.Student.Core.Handlers;

public class CleanAndInitDriveHandler
{
    private static readonly HashSet<string> IgnoredSystemDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "$RECYCLE.BIN",
        "System Volume Information",
        "Recovery",
        "$WinREAgent",
        "Config.Msi"
    };

    public Task<bool> HandleAsync(CleanAndInitDrivePacket packet)
    {
        return Task.Run(() =>
        {
            try
            {
                string targetDrive = string.IsNullOrWhiteSpace(packet.DriveLetter) ? @"D:\" : packet.DriveLetter.Trim();
                if (!targetDrive.EndsWith(Path.DirectorySeparatorChar) && !targetDrive.EndsWith(Path.AltDirectorySeparatorChar))
                {
                    targetDrive += Path.DirectorySeparatorChar;
                }

                string? targetRoot = Path.GetPathRoot(targetDrive);
                if (string.IsNullOrEmpty(targetRoot))
                {
                    Debug.WriteLine($"[CleanAndInitDriveHandler] Không xác định được ổ đĩa: {targetDrive}");
                    return false;
                }

                // 1. Kiểm tra an toàn: Tuyệt đối không cho phép dọn dẹp ổ chứa hệ điều hành
                string systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
                if (string.Equals(targetRoot, systemRoot, StringComparison.OrdinalIgnoreCase) ||
                    targetRoot.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"[CleanAndInitDriveHandler] Từ chối thực thi trên ổ đĩa hệ điều hành: {targetRoot}");
                    return false;
                }

                if (!Directory.Exists(targetRoot))
                {
                    Debug.WriteLine($"[CleanAndInitDriveHandler] Ổ đĩa không tồn tại: {targetRoot}");
                    return false;
                }

                // 2. Dọn dẹp các thư mục con trong ổ mục tiêu (bỏ qua thư mục hệ thống)
                try
                {
                    foreach (string dir in Directory.GetDirectories(targetRoot))
                    {
                        string dirName = Path.GetFileName(dir);
                        if (IgnoredSystemDirectories.Contains(dirName))
                        {
                            continue;
                        }

                        try
                        {
                            Directory.Delete(dir, recursive: true);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[CleanAndInitDriveHandler] Không thể xóa thư mục '{dir}': {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CleanAndInitDriveHandler] Lỗi khi duyệt thư mục tại {targetRoot}: {ex.Message}");
                }

                // 3. Dọn dẹp các tệp tin trong gốc ổ mục tiêu
                try
                {
                    foreach (string file in Directory.GetFiles(targetRoot))
                    {
                        try
                        {
                            // Bỏ thuộc tính ReadOnly/Hidden nếu có để xóa thành công
                            File.SetAttributes(file, FileAttributes.Normal);
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[CleanAndInitDriveHandler] Không thể xóa tệp tin '{file}': {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CleanAndInitDriveHandler] Lỗi khi duyệt tệp tin tại {targetRoot}: {ex.Message}");
                }

                // 4. Khởi tạo danh sách thư mục mới
                if (packet.FolderNames != null && packet.FolderNames.Count > 0)
                {
                    char[] invalidChars = Path.GetInvalidFileNameChars();
                    foreach (string rawName in packet.FolderNames)
                    {
                        if (string.IsNullOrWhiteSpace(rawName))
                            continue;

                        // Làm sạch ký tự không hợp lệ
                        string cleanName = new string(rawName.Trim().Where(c => !invalidChars.Contains(c)).ToArray()).Trim();
                        if (string.IsNullOrEmpty(cleanName))
                            continue;

                        string newFolderPath = Path.Combine(targetRoot, cleanName);
                        try
                        {
                            if (!Directory.Exists(newFolderPath))
                            {
                                Directory.CreateDirectory(newFolderPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[CleanAndInitDriveHandler] Không thể tạo thư mục '{newFolderPath}': {ex.Message}");
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CleanAndInitDriveHandler] Lỗi tổng thể: {ex.Message}");
                return false;
            }
        });
    }
}
