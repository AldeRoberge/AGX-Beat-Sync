using System.Runtime.InteropServices;

namespace AGX_Beat_Sync.Native;

/// <summary>
/// Applies dark mode to the window title bar on Windows 10 1903+ using the same
/// DWM API as <see href="https://github.com/0x7c13/UnityEditor-DarkMode">UnityEditor-DarkMode</see>.
/// </summary>
internal static class TitleBarDarkMode
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>
    /// Enables dark title bar (and border) for the given window handle. No-op on non-Windows or if DWM call fails.
    /// </summary>
    public static void Apply(IntPtr hWnd)
    {
        if (!OperatingSystem.IsWindows() || hWnd == IntPtr.Zero)
            return;

        int useDark = 1;
        try
        {
            DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        }
        catch
        {
            // Ignore; older Windows or missing dwmapi
        }
    }
}
