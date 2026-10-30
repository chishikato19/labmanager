using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace LabManager.Native.System;

public static class DriveSecurityHelper
{
    private static readonly SecurityIdentifier UsersSid = new(WellKnownSidType.BuiltinUsersSid, null);
    private static readonly SecurityIdentifier AuthUsersSid = new(WellKnownSidType.AuthenticatedUserSid, null);

    /// <summary>
    /// Khóa cấu trúc ổ đĩa:
    /// - Không cho tạo file hoặc thư mục mới ở thư mục gốc (VD: D:\).
    /// - Không cho xóa, không cho đổi tên, không cho di chuyển (kéo thả) các thư mục hiện có trên ổ đĩa.
    /// - Cho phép toàn quyền thao tác (tạo, sửa, lưu bài làm, xóa tệp con) BÊN TRONG các thư mục đã có.
    /// </summary>
    public static bool LockDrive(string driveLetter = @"D:\")
    {
        try
        {
            string normalizedDrive = NormalizeDrivePath(driveLetter);
            if (!Directory.Exists(normalizedDrive))
            {
                return false;
            }

            var rootInfo = new DirectoryInfo(normalizedDrive);

            // 1. Khóa thư mục gốc ổ đĩa: Cấm học sinh tạo mới file hoặc thư mục ngay tại gốc
            ApplyRootLock(normalizedDrive, rootInfo);

            // 2. Khóa chống xóa / đổi tên / kéo thả cho tất cả các thư mục có sẵn
            foreach (var subDir in rootInfo.GetDirectories())
            {
                if (IsSystemOrHiddenDirectory(subDir))
                {
                    continue;
                }

                LockFolderStructure(subDir.FullName);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Mở khóa ổ đĩa:
    /// - Cho phép tạo file/folder bình thường ở gốc ổ đĩa.
    /// - Cho phép xóa/đổi tên các thư mục bình thường.
    /// </summary>
    public static bool UnlockDrive(string driveLetter = @"D:\")
    {
        try
        {
            string normalizedDrive = NormalizeDrivePath(driveLetter);
            if (!Directory.Exists(normalizedDrive))
            {
                return false;
            }

            var rootInfo = new DirectoryInfo(normalizedDrive);

            // 1. Gỡ bỏ khóa ở gốc ổ đĩa
            ApplyRootUnlock(normalizedDrive, rootInfo);

            // 2. Gỡ bỏ khóa, khôi phục quyền kế thừa chuẩn cho các thư mục con
            foreach (var subDir in rootInfo.GetDirectories())
            {
                if (IsSystemOrHiddenDirectory(subDir))
                {
                    continue;
                }

                UnlockFolderStructure(subDir.FullName);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyRootLock(string drivePath, DirectoryInfo rootInfo)
    {
        try
        {
            var rootAcl = rootInfo.GetAccessControl();
            RemoveDenyRules(rootAcl, UsersSid, FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories);
            RemoveDenyRules(rootAcl, AuthUsersSid, FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories);

            var denyRootUsers = new FileSystemAccessRule(
                UsersSid,
                FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories,
                InheritanceFlags.None,
                PropagationFlags.None,
                AccessControlType.Deny);

            var denyRootAuth = new FileSystemAccessRule(
                AuthUsersSid,
                FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories,
                InheritanceFlags.None,
                PropagationFlags.None,
                AccessControlType.Deny);

            rootAcl.AddAccessRule(denyRootUsers);
            rootAcl.AddAccessRule(denyRootAuth);
            rootInfo.SetAccessControl(rootAcl);
        }
        catch
        {
            RunIcacls($"\"{drivePath}\" /deny *S-1-5-32-545:(DE,WD,AD) *S-1-5-11:(DE,WD,AD)");
        }
    }

    private static void ApplyRootUnlock(string drivePath, DirectoryInfo rootInfo)
    {
        try
        {
            var rootAcl = rootInfo.GetAccessControl();
            RemoveDenyRules(rootAcl, UsersSid, FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories);
            RemoveDenyRules(rootAcl, AuthUsersSid, FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories);
            rootInfo.SetAccessControl(rootAcl);
        }
        catch
        {
            RunIcacls($"\"{drivePath}\" /remove:d *S-1-5-32-545 /remove:d *S-1-5-11");
        }
    }

    /// <summary>
    /// Khóa cấu trúc một thư mục:
    /// - Ngắt kế thừa từ ổ D:\ để cắt sạch quyền Write/Delete thừa hưởng.
    /// - Vỏ thư mục (chính nó): Chỉ cho Read/Traverse (RX), CẤM Delete (DE) và WriteAttributes (WA).
    /// - Ruột thư mục (file & folder con): Cấp toàn quyền Modify (OI)(CI)(IO)M.
    /// </summary>
    public static void LockFolderStructure(string folderPath)
    {
        try
        {
            // 1. Tách kế thừa nhưng copy rule cũ (giữ Admin/SYSTEM toàn quyền)
            RunIcacls($"\"{folderPath}\" /inheritance:d");

            // 2. Gỡ bỏ quyền Write/Delete của Users & Authenticated Users trên vỏ thư mục
            RunIcacls($"\"{folderPath}\" /remove:g *S-1-5-32-545 /remove:g *S-1-5-11");

            // 3. Cấp quyền Đọc & Duyệt cho vỏ thư mục (chính nó)
            RunIcacls($"\"{folderPath}\" /grant *S-1-5-32-545:(RX) *S-1-5-11:(RX)");

            // 4. Cấp quyền Modify bên trong (áp dụng cho toàn bộ file/folder con)
            RunIcacls($"\"{folderPath}\" /grant *S-1-5-32-545:(OI)(CI)(IO)M *S-1-5-11:(OI)(CI)(IO)M");

            // 5. Khóa cứng vỏ: CẤM Xóa (DE) và cấm Đổi tên/Ghi thuộc tính (WA) trên chính thư mục này
            RunIcacls($"\"{folderPath}\" /deny *S-1-5-32-545:(DE,WA) *S-1-5-11:(DE,WA)");
        }
        catch
        {
            // Bỏ qua nếu gặp lỗi thư mục đang bận
        }
    }

    /// <summary>
    /// Mở khóa khôi phục quyền mặc định cho thư mục
    /// </summary>
    public static void UnlockFolderStructure(string folderPath)
    {
        try
        {
            // Gỡ bỏ DENY Delete và trả lại kế thừa tự nhiên từ ổ đĩa
            RunIcacls($"\"{folderPath}\" /remove:d *S-1-5-32-545 /remove:d *S-1-5-11");
            RunIcacls($"\"{folderPath}\" /reset");
        }
        catch
        {
            // Bỏ qua
        }
    }

    private static void RunIcacls(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "icacls.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);
        }
        catch
        {
            // Bỏ qua nếu lỗi tiến trình
        }
    }

    private static string NormalizeDrivePath(string drive)
    {
        if (string.IsNullOrWhiteSpace(drive)) return @"D:\";
        string clean = drive.Trim().ToUpperInvariant();
        if (clean.Length == 1 && char.IsLetter(clean[0]))
        {
            return clean + @":\";
        }
        if (!clean.EndsWith(@"\"))
        {
            clean += @"\";
        }
        return clean;
    }

    private static bool IsSystemOrHiddenDirectory(DirectoryInfo dir)
    {
        if (dir.Name.StartsWith("$", StringComparison.OrdinalIgnoreCase)) return true;
        if (dir.Name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)) return true;
        if (dir.Name.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase)) return true;

        var attrs = dir.Attributes;
        if ((attrs & FileAttributes.Hidden) != 0 && (attrs & FileAttributes.System) != 0)
        {
            return true;
        }

        return false;
    }

    private static void RemoveDenyRules(DirectorySecurity acl, SecurityIdentifier sid, FileSystemRights rightsToCheck)
    {
        var rules = acl.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier));
        foreach (FileSystemAccessRule rule in rules)
        {
            if (rule.IdentityReference == sid && rule.AccessControlType == AccessControlType.Deny)
            {
                if ((rule.FileSystemRights & rightsToCheck) != 0)
                {
                    acl.RemoveAccessRule(rule);
                }
            }
        }
    }
}
