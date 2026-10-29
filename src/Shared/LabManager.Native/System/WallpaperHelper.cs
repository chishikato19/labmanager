using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace LabManager.Native.System;

public static class WallpaperHelper
{
    private const int SPI_SETDESKWALLPAPER = 0x0014;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDCHANGE = 0x02;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    /// <summary>
    /// Đổi hình nền Desktop cho phiên người dùng hiện tại
    /// </summary>
    /// <param name="imagePath">Đường dẫn tệp ảnh tuyệt đối</param>
    /// <param name="style">10: Fill, 2: Fit, 6: Stretch, 0: Center</param>
    /// <returns>True nếu thành công</returns>
    public static bool SetWallpaper(string imagePath, int style = 10)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return false;

        try
        {
            // Thiết lập WallpaperStyle và TileWallpaper trong Registry HKCU\Control Panel\Desktop
            using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
            {
                if (key != null)
                {
                    string styleStr = style switch
                    {
                        10 => "10", // Fill
                        2 => "2",   // Fit
                        6 => "6",   // Stretch
                        0 => "0",   // Center
                        _ => "10"
                    };

                    key.SetValue("WallpaperStyle", styleStr);
                    key.SetValue("TileWallpaper", "0");
                }
            }

            // Gọi SystemParametersInfo để Windows cập nhật và thông báo cho Explorer/Shell làm mới ngay lập tức
            int result = SystemParametersInfo(
                SPI_SETDESKWALLPAPER,
                0,
                imagePath,
                SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

            return result != 0;
        }
        catch
        {
            return false;
        }
    }
}
