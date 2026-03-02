using System.Runtime.InteropServices;

namespace AGX_Beat_Sync.Native;

/// <summary>Pending clipboard action from WM_COPY / WM_CUT / WM_PASTE, to be processed on the game thread in Update().</summary>
public enum PendingClipboardAction
{
    None,
    Copy,
    Cut,
    Paste
}

/// <summary>Subclasses a Win32 window to handle WM_COPY, WM_CUT, WM_PASTE so Ctrl+C/X/V work when the OS consumes key events. Windows-only.</summary>
internal sealed class WindowClipboardHandler
{
    private IntPtr _originalWndProc;
    private readonly object _lock = new();
    private PendingClipboardAction _pending = PendingClipboardAction.None;

    // Keep delegate alive so it is not GC'd while the window is subclassed
    private WndProcDelegate? _wndProcDelegate;

    private const int WM_COPY = 0x0301;
    private const int WM_CUT = 0x0300;
    private const int WM_PASTE = 0x0302;
    private const int GWLP_WNDPROC = -4;

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr wndProc);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr wndProc);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetLastError();

    [DllImport("kernel32.dll")]
    private static extern void SetLastError(uint dwErrCode);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr wndProc) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, wndProc) : SetWindowLongPtr32(hWnd, nIndex, wndProc);

    /// <summary>Subclass the given HWND so WM_COPY/CUT/PASTE set a pending action. Returns true if successful.</summary>
    public bool Install(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !OperatingSystem.IsWindows())
            return false;

        _originalWndProc = GetWindowLongPtr(hwnd, GWLP_WNDPROC);
        if (_originalWndProc == IntPtr.Zero)
            return false;

        _wndProcDelegate = CustomWndProc;
        IntPtr newProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
        SetLastError(0);
        IntPtr prevProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, newProc);
        // Zero return can mean success (previous proc was null) or failure; use GetLastError to distinguish
        if (prevProc == IntPtr.Zero && GetLastError() != 0)
            return false;

        return true;
    }

    private IntPtr CustomWndProc(IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam)
    {
        if (uMsg == WM_COPY)
        {
            lock (_lock) { _pending = PendingClipboardAction.Copy; }
            return IntPtr.Zero;
        }
        if (uMsg == WM_CUT)
        {
            lock (_lock) { _pending = PendingClipboardAction.Cut; }
            return IntPtr.Zero;
        }
        if (uMsg == WM_PASTE)
        {
            lock (_lock) { _pending = PendingClipboardAction.Paste; }
            return IntPtr.Zero;
        }
        return CallWindowProc(_originalWndProc, hwnd, uMsg, wParam, lParam);
    }

    /// <summary>Returns the pending clipboard action and clears it. Call from the game thread (e.g. at start of Update).</summary>
    public PendingClipboardAction TryTakePendingAction()
    {
        lock (_lock)
        {
            var a = _pending;
            _pending = PendingClipboardAction.None;
            return a;
        }
    }
}
