using System.Runtime.InteropServices;

namespace LabManager.Native.Input;

public static class InputSimulator
{
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public static void MoveMouse(double normX, double normY)
    {
        int screenW = GetSystemMetrics(SM_CXSCREEN);
        int screenH = GetSystemMetrics(SM_CYSCREEN);
        int targetX = (int)Math.Clamp(normX * screenW, 0, screenW - 1);
        int targetY = (int)Math.Clamp(normY * screenH, 0, screenH - 1);
        SetCursorPos(targetX, targetY);
    }

    public static void MouseButton(double normX, double normY, int button, bool isDown)
    {
        MoveMouse(normX, normY);
        uint flag = button switch
        {
            1 => isDown ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP,
            2 => isDown ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP,
            _ => isDown ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP
        };
        mouse_event(flag, 0, 0, 0, UIntPtr.Zero);
    }

    public static void MouseWheel(int delta)
    {
        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)delta, UIntPtr.Zero);
    }

    public static void KeyEvent(int virtualKey, bool isDown)
    {
        uint flag = isDown ? 0 : KEYEVENTF_KEYUP;
        keybd_event((byte)virtualKey, 0, flag, UIntPtr.Zero);
    }
}
