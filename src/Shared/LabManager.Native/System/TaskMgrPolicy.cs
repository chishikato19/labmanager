using Microsoft.Win32;

namespace LabManager.Native.System;

public static class TaskMgrPolicy
{
    private const string SubKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string ValueName = "DisableTaskMgr";

    public static void SetTaskMgrDisabled(bool disabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(SubKey);
            if (key == null) return;

            if (disabled)
            {
                key.SetValue(ValueName, 1, RegistryValueKind.DWord);
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }
        }
        catch
        {
            // Bỏ qua nếu không có quyền ghi registry
        }
    }
}
