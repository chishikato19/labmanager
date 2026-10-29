using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace LabManager.Native.System;

public static class BrowserLauncherHelper
{
    public static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "https://www.google.com";

        string cleanUrl = url.Trim();

        if (!cleanUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !cleanUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !cleanUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            cleanUrl = "https://" + cleanUrl;
        }

        return cleanUrl;
    }

    public static string? FindChromePath()
    {
        // 1. Kiểm tra Registry App Paths
        string[] registryKeys =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe"
        ];

        foreach (var keyPath in registryKeys)
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

        // 2. Kiểm tra các thư mục cài đặt tiêu chuẩn của Google Chrome
        string[] potentialPaths =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe")
        ];

        foreach (var path in potentialPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public static bool OpenUrl(string url, bool preferChrome = true)
    {
        string normalizedUrl = NormalizeUrl(url);

        if (preferChrome)
        {
            string? chromePath = FindChromePath();
            if (!string.IsNullOrWhiteSpace(chromePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = chromePath,
                        Arguments = $"\"{normalizedUrl}\"",
                        UseShellExecute = true
                    });
                    return true;
                }
                catch
                {
                    // Chuyển sang fallback nếu không chạy được Chrome
                }
            }
        }

        // Fallback: Mở qua trình duyệt mặc định của hệ thống
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = normalizedUrl,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            try
            {
                // Fallback cuối: Microsoft Edge
                Process.Start(new ProcessStartInfo
                {
                    FileName = "msedge.exe",
                    Arguments = $"\"{normalizedUrl}\"",
                    UseShellExecute = true
                });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
