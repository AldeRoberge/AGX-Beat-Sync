using System.Runtime.InteropServices;

namespace AGX_Beat_Sync.Native;

/// <summary>
/// Gets the Win32 HWND from a MonoGame DesktopGL (SDL2) window. MonoGame's Window.Handle is the SDL_Window*,
/// not the HWND, so GetForegroundWindow() and SetForegroundWindow() must use the real HWND from SDL_GetWindowWMInfo.
/// </summary>
internal static class SdlWin32
{
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int SDL_GetWindowWMInfo(IntPtr window, ref SDL_SysWMinfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct SDL_version
    {
        public byte major;
        public byte minor;
        public byte patch;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SDL_SysWMinfo
    {
        public SDL_version version;
        private byte _padding;
        public int subsystem; // SDL_SysWMType
        public WinInfo win;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinInfo
    {
        public IntPtr window;  // HWND
        public IntPtr hdc;
        public IntPtr hinstance;
    }

    /// <summary>Gets the Win32 HWND for the given SDL window pointer (GameWindow.Handle on DesktopGL). Returns true if successful. Tries several SDL version structs so one matches the runtime SDL.</summary>
    public static bool TryGetHwndFromSdlWindow(IntPtr sdlWindow, out IntPtr hwnd)
    {
        hwnd = IntPtr.Zero;
        if (sdlWindow == IntPtr.Zero || !OperatingSystem.IsWindows()) return false;
        // SDL_GetWindowWMInfo requires the version in the struct to match the loaded SDL; try common versions used by MonoGame/SDL2.
        var versions = new[] { (2, 0, 28), (2, 0, 26), (2, 0, 24), (2, 0, 22), (2, 0, 20), (2, 0, 18), (2, 0, 14), (2, 0, 12), (2, 0, 10), (2, 0, 8), (2, 0, 6), (2, 0, 4), (2, 0, 0) };
        foreach (var (major, minor, patch) in versions)
        {
            var info = new SDL_SysWMinfo
            {
                version = new SDL_version { major = (byte)major, minor = (byte)minor, patch = (byte)patch }
            };
            try
            {
                if (SDL_GetWindowWMInfo(sdlWindow, ref info) != 0)
                {
                    hwnd = info.win.window;
                    if (hwnd != IntPtr.Zero)
                        return true;
                }
            }
            catch { /* try next version */ }
        }
        return false;
    }
}
