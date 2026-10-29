using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using LabManager.Core.Interfaces;

namespace LabManager.Native.Screen;

public class GdiScreenCapturer : IScreenCapturer
{
    private static readonly ImageCodecInfo JpegCodec = GetJpegCodec();
    private bool _isDisposed;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDPIAware();

    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = (IntPtr)(-4);

    static GdiScreenCapturer()
    {
        try
        {
            if (!SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
            {
                SetProcessDPIAware();
            }
        }
        catch
        {
            try { SetProcessDPIAware(); } catch { }
        }
    }

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    public bool Initialize() => true;
    public bool CaptureCursor { get; set; } = true;
    public bool LaserPointerEnabled { get; set; }

    public byte[]? CaptureFrameJpeg(int quality = 50, int? targetWidth = null, int? targetHeight = null)
    {
        if (_isDisposed) return null;

        int screenWidth = GetSystemMetrics(SM_CXSCREEN);
        int screenHeight = GetSystemMetrics(SM_CYSCREEN);

        if (screenWidth <= 0 || screenHeight <= 0)
        {
            screenWidth = 1920;
            screenHeight = 1080;
        }

        int outWidth = targetWidth ?? screenWidth;
        int outHeight = targetHeight ?? screenHeight;

        try
        {
            using var sourceBitmap = new Bitmap(screenWidth, screenHeight, PixelFormat.Format32bppRgb);
            using (var g = Graphics.FromImage(sourceBitmap))
            {
                g.CopyFromScreen(0, 0, 0, 0, new Size(screenWidth, screenHeight), CopyPixelOperation.SourceCopy);
                if (CaptureCursor)
                {
                    CursorDrawer.DrawCursor(g, LaserPointerEnabled);
                }
            }

            Bitmap finalBitmap;
            bool isResized = (outWidth != screenWidth || outHeight != screenHeight);

            if (isResized)
            {
                finalBitmap = new Bitmap(outWidth, outHeight, PixelFormat.Format24bppRgb);
                using var resizeGraphics = Graphics.FromImage(finalBitmap);
                resizeGraphics.InterpolationMode = InterpolationMode.Bilinear;
                resizeGraphics.DrawImage(sourceBitmap, 0, 0, outWidth, outHeight);
            }
            else
            {
                finalBitmap = sourceBitmap;
            }

            using var ms = new MemoryStream();
            using var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);

            finalBitmap.Save(ms, JpegCodec, encoderParams);

            if (isResized)
            {
                finalBitmap.Dispose();
            }

            return ms.ToArray();
        }
        catch (Win32Exception)
        {
            // Fallback khi chạy trong session không có màn hình vật lý (headless, service, UAC, lock screen)
            return GenerateFallbackFrame(outWidth, outHeight, quality);
        }
        catch (Exception)
        {
            return GenerateFallbackFrame(outWidth, outHeight, quality);
        }
    }

    private static byte[] GenerateFallbackFrame(int width, int height, int quality)
    {
        using var fallback = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(fallback))
        {
            g.Clear(Color.FromArgb(15, 23, 42)); // Slate dark #0F172A

            using var brush = new SolidBrush(Color.FromArgb(148, 163, 184));
            using var font = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Regular);
            string text = $"[Đang bảo vệ phiên]\n{Environment.MachineName}\n{DateTime.Now:HH:mm:ss}";
            g.DrawString(text, font, brush, new PointF(10, 10));
        }

        using var ms = new MemoryStream();
        using var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
        fallback.Save(ms, JpegCodec, encoderParams);
        return ms.ToArray();
    }

    private static ImageCodecInfo GetJpegCodec()
    {
        return ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
    }

    public void Dispose()
    {
        _isDisposed = true;
    }
}
