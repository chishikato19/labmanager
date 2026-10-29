using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace LabManager.Native.System;

public static class AppLauncherHelper
{
    private static readonly Dictionary<string, string> AppAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "word", "winword.exe" },
        { "winword", "winword.exe" },
        { "excel", "excel.exe" },
        { "powerpoint", "powerpnt.exe" },
        { "powerpnt", "powerpnt.exe" },
        { "ppt", "powerpnt.exe" },
        { "vscode", "code.exe" },
        { "code", "code.exe" },
        { "visual studio code", "code.exe" },
        { "notepad", "notepad.exe" },
        { "calc", "calc.exe" },
        { "calculator", "calc.exe" },
        { "paint", "mspaint.exe" },
        { "mspaint", "mspaint.exe" },
        { "scratch", "scratch.exe" },
        { "scratch desktop", "scratch desktop.exe" },
        { "cmd", "cmd.exe" },
        { "powershell", "powershell.exe" },
        { "idle", "idle.exe" },
        { "python", "python.exe" },
        { "devcpp", "devcpp.exe" },
        { "codeblocks", "codeblocks.exe" },
        { "geogebra", "geogebra.exe" }
    };

    public static string ResolveAppNameOrPath(string appNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(appNameOrPath))
            return string.Empty;

        string trimmed = appNameOrPath.Trim().Trim('"');

        if (AppAliases.TryGetValue(trimmed, out var mappedExe))
        {
            trimmed = mappedExe;
        }

        return trimmed;
    }

    public static string? FindExecutable(string appNameOrPath)
    {
        string target = ResolveAppNameOrPath(appNameOrPath);
        if (string.IsNullOrWhiteSpace(target)) return null;

        // 1. Kiểm tra trực tiếp đường dẫn file
        if (File.Exists(target))
            return target;

        string exeName = target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? target : target + ".exe";

        // 2. Tra cứu Registry App Paths
        string[] appPathKeys =
        [
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exeName}",
            $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\{exeName}"
        ];

        foreach (var keyPath in appPathKeys)
        {
            try
            {
                using var hklmKey = Registry.LocalMachine.OpenSubKey(keyPath);
                var hklmVal = hklmKey?.GetValue("") as string;
                if (!string.IsNullOrWhiteSpace(hklmVal) && File.Exists(hklmVal))
                {
                    return hklmVal;
                }

                using var hkcuKey = Registry.CurrentUser.OpenSubKey(keyPath);
                var hkcuVal = hkcuKey?.GetValue("") as string;
                if (!string.IsNullOrWhiteSpace(hkcuVal) && File.Exists(hkcuVal))
                {
                    return hkcuVal;
                }
            }
            catch { }
        }

        // 3. Tra cứu các phần mềm phổ biến trong thư mục tiêu chuẩn
        if (exeName.Equals("code.exe", StringComparison.OrdinalIgnoreCase))
        {
            string vsCodeLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Microsoft VS Code\Code.exe");
            if (File.Exists(vsCodeLocal)) return vsCodeLocal;

            string vsCodePf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft VS Code\Code.exe");
            if (File.Exists(vsCodePf)) return vsCodePf;
        }
        else if (exeName.Contains("scratch", StringComparison.OrdinalIgnoreCase))
        {
            string scratchLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Scratch Desktop\Scratch Desktop.exe");
            if (File.Exists(scratchLocal)) return scratchLocal;

            string scratchPf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Scratch Desktop\Scratch Desktop.exe");
            if (File.Exists(scratchPf)) return scratchPf;
        }
        else if (exeName.Equals("devcpp.exe", StringComparison.OrdinalIgnoreCase))
        {
            string devCppPath = @"C:\Program Files (x86)\Embarcadero\Dev-Cpp\devcpp.exe";
            if (File.Exists(devCppPath)) return devCppPath;

            string devCppOld = @"C:\Program Files (x86)\Dev-Cpp\devcpp.exe";
            if (File.Exists(devCppOld)) return devCppOld;
        }
        else if (exeName.Equals("codeblocks.exe", StringComparison.OrdinalIgnoreCase))
        {
            string cbPath = @"C:\Program Files\CodeBlocks\codeblocks.exe";
            if (File.Exists(cbPath)) return cbPath;

            string cbPf86 = @"C:\Program Files (x86)\CodeBlocks\codeblocks.exe";
            if (File.Exists(cbPf86)) return cbPf86;
        }

        return exeName;
    }

    public static bool LaunchApp(string appNameOrPath, string arguments = "")
    {
        if (string.IsNullOrWhiteSpace(appNameOrPath))
            return false;

        string? resolved = FindExecutable(appNameOrPath);
        string target = !string.IsNullOrWhiteSpace(resolved) ? resolved : ResolveAppNameOrPath(appNameOrPath);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = target,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            try
            {
                // Thử chạy trực tiếp với chuỗi gốc
                var rawPsi = new ProcessStartInfo
                {
                    FileName = appNameOrPath.Trim().Trim('"'),
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true
                };
                Process.Start(rawPsi);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
