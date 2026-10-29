namespace LabManager.Core.Interfaces;

public interface IScreenCapturer : IDisposable
{
    bool Initialize();
    byte[]? CaptureFrameJpeg(int quality = 60, int? targetWidth = null, int? targetHeight = null);
    bool CaptureCursor { get; set; }
    bool LaserPointerEnabled { get; set; }
    int ScreenWidth { get; }
    int ScreenHeight { get; }
}
