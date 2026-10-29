using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace LabManager.Native.Screen;

public static class CursorDrawer
{
    private const int CURSOR_SHOWING = 0x00000001;
    private const uint DI_NORMAL = 0x0003;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(ref CURSORINFO pci);

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("user32.dll")]
    private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyWidth, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    public static void DrawCursor(Graphics g, bool laserPointer)
    {
        var info = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(ref info) || (info.flags & CURSOR_SHOWING) == 0 || info.hCursor == IntPtr.Zero)
            return;

        int x = info.ptScreenPos.x;
        int y = info.ptScreenPos.y;

        if (laserPointer)
            DrawLaser(g, x, y);
        else
            DrawPointer(g, info, x, y);
    }

    private static void DrawLaser(Graphics g, int x, int y)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var b1 = new SolidBrush(Color.FromArgb(45, 239, 68, 68));
        g.FillEllipse(b1, x - 20, y - 20, 40, 40);

        using var b2 = new SolidBrush(Color.FromArgb(130, 239, 68, 68));
        g.FillEllipse(b2, x - 12, y - 12, 24, 24);

        using var b3 = new SolidBrush(Color.FromArgb(230, 220, 38, 38));
        g.FillEllipse(b3, x - 6, y - 6, 12, 12);

        using var b4 = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
        g.FillEllipse(b4, x - 2, y - 2, 4, 4);
    }

    private static void DrawPointer(Graphics g, CURSORINFO info, int x, int y)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var halo = new SolidBrush(Color.FromArgb(70, 250, 204, 21));
        g.FillEllipse(halo, x - 14, y - 14, 28, 28);

        int dx = x, dy = y;
        if (GetIconInfo(info.hCursor, out var ii))
        {
            dx = x - ii.xHotspot;
            dy = y - ii.yHotspot;
            if (ii.hbmMask != IntPtr.Zero) DeleteObject(ii.hbmMask);
            if (ii.hbmColor != IntPtr.Zero) DeleteObject(ii.hbmColor);
        }

        IntPtr hdc = g.GetHdc();
        try { DrawIconEx(hdc, dx, dy, info.hCursor, 0, 0, 0, IntPtr.Zero, DI_NORMAL); }
        catch { }
        finally { g.ReleaseHdc(hdc); }
    }
}
