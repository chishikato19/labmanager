using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace LabManager.Native.System;

public static class DriveSecurityHelper
{
    private static readonly SecurityIdentifier UsersSid = new(WellKnownSidType.BuiltinUsersSid, null);

    /// <summary>
    /// Khóa cấu trúc ổ đĩa:
    /// - Không cho tạo file hoặc thư mục mới ở thư mục gốc (VD: D:\).
    /// - Không cho xóa hoặc đổi tên các thư mục hiện có trên ổ đĩa.
    /// - Cho phép toàn quyền thao tác (tạo, sửa, xóa file/folder) bên trong các thư mục đã có.
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
            var rootAcl = rootInfo.GetAccessControl();

            // 1. Gỡ bỏ các rule Deny cũ trên gốc (nếu có) để tránh trùng lặp
            RemoveDenyRules(rootAcl, UsersSid, FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories);

            // 2. Cấm tạo mới file và thư mục ngay tại gốc ổ đĩa (InheritanceFlags.None: Không kế thừa xuống con)
            var denyRootCreate = new FileSystemAccessRule(
                UsersSid,
                FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories,
                InheritanceFlags.None,
                PropagationFlags.None,
                AccessControlType.Deny);

            rootAcl.AddAccessRule(denyRootCreate);
            rootInfo.SetAccessControl(rootAcl);

            // 3. Khóa chống xóa / đổi tên cho tất cả các thư mục hợp lệ hiện có trên ổ đĩa
            foreach (var subDir in rootInfo.GetDirectories())
            {
                if (IsSystemOrHiddenDirectory(subDir))
                {
                    continue;
                }

                try
                {
                    var dirAcl = subDir.GetAccessControl();

                    // Gỡ bỏ Deny Delete cũ
                    RemoveDenyRules(dirAcl, UsersSid, FileSystemRights.Delete);

                    // A. Cấm xóa và đổi tên chính thư mục này (InheritanceFlags.None: Không cấm các file bên trong)
                    var denyDeleteSelf = new FileSystemAccessRule(
                        UsersSid,
                        FileSystemRights.Delete,
                        InheritanceFlags.None,
                        PropagationFlags.None,
                        AccessControlType.Deny);

                    // B. Cấp quyền Modify cho toàn bộ file và thư mục con bên trong
                    var allowChildrenModify = new FileSystemAccessRule(
                        UsersSid,
                        FileSystemRights.Modify,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.InheritOnly,
                        AccessControlType.Allow);

                    dirAcl.AddAccessRule(denyDeleteSelf);
                    dirAcl.AddAccessRule(allowChildrenModify);
                    subDir.SetAccessControl(dirAcl);
                }
                catch
                {
                    // Tiếp tục với các thư mục khác nếu gặp lỗi ở 1 thư mục cụ thể
                }
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
            var rootAcl = rootInfo.GetAccessControl();

            // 1. Gỡ bỏ Deny tạo file/thư mục ở gốc
            RemoveDenyRules(rootAcl, UsersSid, FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories);
            rootInfo.SetAccessControl(rootAcl);

            // 2. Gỡ bỏ Deny xóa cho các thư mục con
            foreach (var subDir in rootInfo.GetDirectories())
            {
                if (IsSystemOrHiddenDirectory(subDir))
                {
                    continue;
                }

                try
                {
                    var dirAcl = subDir.GetAccessControl();
                    RemoveDenyRules(dirAcl, UsersSid, FileSystemRights.Delete);
                    subDir.SetAccessControl(dirAcl);
                }
                catch
                {
                    // Tiếp tục với các thư mục khác
                }
            }

            return true;
        }
        catch
        {
            return false;
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
