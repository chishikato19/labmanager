using System.Diagnostics;
using System.Text;

namespace LabManager.Native.System;

public static class HostsFileHelper
{
    private const string StartTag = "# --- LABMANAGER WEB FILTER START ---";
    private const string EndTag = "# --- LABMANAGER WEB FILTER END ---";

    public static string HostsFilePath { get; set; } = 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");

    public static void ApplyBlockedDomains(IEnumerable<string> domains)
    {
        string currentContent = File.Exists(HostsFilePath) ? File.ReadAllText(HostsFilePath) : string.Empty;
        string cleanedContent = RemoveFilterSection(currentContent);

        var domainList = domains
            .Select(CleanDomain)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (domainList.Count == 0)
        {
            File.WriteAllText(HostsFilePath, cleanedContent, Encoding.UTF8);
            FlushDns();
            return;
        }

        var sb = new StringBuilder(cleanedContent.TrimEnd());
        sb.AppendLine();
        sb.AppendLine(StartTag);

        foreach (var domain in domainList)
        {
            sb.AppendLine($"127.0.0.1 {domain}");
            if (!domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine($"127.0.0.1 www.{domain}");
            }
        }

        sb.AppendLine(EndTag);
        File.WriteAllText(HostsFilePath, sb.ToString(), Encoding.UTF8);
        FlushDns();
    }

    public static void ClearBlockedDomains()
    {
        try
        {
            if (!File.Exists(HostsFilePath)) return;

            string content = File.ReadAllText(HostsFilePath);
            string cleaned = RemoveFilterSection(content);
            File.WriteAllText(HostsFilePath, cleaned, Encoding.UTF8);
            FlushDns();
        }
        catch (UnauthorizedAccessException)
        {
            // Bỏ qua nếu không có quyền ghi hosts file (chạy test không có quyền admin)
        }
        catch
        {
            // Bỏ qua lỗi truy cập file khác
        }
    }

    public static string RemoveFilterSection(string text)
    {
        int startIndex = text.IndexOf(StartTag, StringComparison.OrdinalIgnoreCase);
        if (startIndex == -1) return text;

        int endIndex = text.IndexOf(EndTag, StringComparison.OrdinalIgnoreCase);
        if (endIndex == -1) return text[..startIndex].TrimEnd();

        string before = text[..startIndex];
        string after = text[(endIndex + EndTag.Length)..];
        return (before + after).TrimEnd() + Environment.NewLine;
    }

    private static string CleanDomain(string raw)
    {
        string d = raw.Trim().ToLowerInvariant();
        if (d.StartsWith("http://")) d = d[7..];
        if (d.StartsWith("https://")) d = d[8..];
        int slashIdx = d.IndexOf('/');
        if (slashIdx >= 0) d = d[..slashIdx];
        return d.Trim();
    }

    private static void FlushDns()
    {
        try
        {
            var psi = new ProcessStartInfo("ipconfig", "/flushdns")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi)?.WaitForExit(1000);
        }
        catch { }
    }
}
