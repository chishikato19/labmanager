using LabManager.Native.System;
using LabManager.Protocol.Packets;

namespace LabManager.Student.Core.Handlers;

public class WallpaperHandler
{
    public async Task<bool> HandleWallpaperAsync(SetWallpaperPacket packet)
    {
        if (packet.ImageBytes == null || packet.ImageBytes.Length == 0)
            return false;

        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string wallpaperDir = Path.Combine(appData, "LabManager", "Wallpaper");
            Directory.CreateDirectory(wallpaperDir);

            string ext = Path.GetExtension(packet.FileName);
            if (string.IsNullOrWhiteSpace(ext))
                ext = ".jpg";

            string targetPath = Path.Combine(wallpaperDir, $"custom_wallpaper{ext}");

            // Ghi đè tệp ảnh hình nền
            await File.WriteAllBytesAsync(targetPath, packet.ImageBytes);

            // Thiết lập hình nền thông qua Win32 API
            return WallpaperHelper.SetWallpaper(targetPath, packet.Style);
        }
        catch
        {
            return false;
        }
    }
}
