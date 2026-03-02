using System.Runtime.InteropServices;

namespace AGX_Beat_Sync.Native;

/// <summary>
/// Win32 helpers for window state and monitor work area. Used when the window is maximized
/// so layout uses the visible work area (excluding taskbar) and content is not cut off.
/// </summary>
internal static class WindowWorkArea
{
    private const int MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    /// <summary>
    /// When the window is maximized, returns the monitor work area size (width and height)
    /// so the app can size its layout to fit within the visible area and not hide the status bar.
    /// Returns false when not on Windows, hwnd is zero, or the window is not maximized.
    /// </summary>
    public static bool TryGetMaximizedWorkAreaSize(IntPtr hwnd, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero || !IsZoomed(hwnd))
            return false;

        IntPtr mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (mon == IntPtr.Zero) return false;

        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(mon, ref mi)) return false;

        width = Math.Max(0, mi.rcWork.right - mi.rcWork.left);
        height = Math.Max(0, mi.rcWork.bottom - mi.rcWork.top);
        return width > 0 && height > 0;
    }
}
