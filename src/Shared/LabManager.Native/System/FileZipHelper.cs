using System.IO.Compression;

namespace LabManager.Native.System;

public static class FileZipHelper
{
    public static byte[] CreateZipFromDirectory(string sourceDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            Directory.CreateDirectory(sourceDirectory);
        }

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var files = Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                string relPath = Path.GetRelativePath(sourceDirectory, file);
                archive.CreateEntryFromFile(file, relPath, CompressionLevel.Optimal);
            }
        }

        return ms.ToArray();
    }

    public static void ExtractZipToDirectory(byte[] zipBytes, string targetDirectory)
    {
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        string fullTarget = Path.GetFullPath(targetDirectory);
        using var ms = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // thư mục rỗng

            string destPath = Path.GetFullPath(Path.Combine(fullTarget, entry.FullName));
            // Ngăn chặn Zip Slip vulnerability
            if (!destPath.StartsWith(fullTarget, StringComparison.OrdinalIgnoreCase))
                continue;

            string? dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            entry.ExtractToFile(destPath, overwrite: true);
        }
    }
}
