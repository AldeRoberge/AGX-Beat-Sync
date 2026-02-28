using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.Input;

/// <summary>
/// Centralized input: mouse, keyboard, drag state.
/// Call Update() once per frame at the start of Update.
/// Keyboard state merges GameWindow KeyDown/KeyUp events with Keyboard.GetState() so shortcuts work regardless of backend.
/// </summary>
public class InputManager
{
    private MouseState _mousePrev;
    private KeyboardState _keyPrev;
    private readonly HashSet<Keys> _eventKeysDown = new();
    private readonly object _keysLock = new();
    // Win32 fallback: detect edit shortcut key transition when KeyDown/merged state miss it (e.g. SDL not delivering)
    private bool _win32PrevZ, _win32PrevY, _win32PrevC, _win32PrevX, _win32PrevV, _win32PrevA;

    public MouseState Mouse { get; private set; }
    public KeyboardState Keyboard { get; private set; }

    /// <summary>Called from GameWindow.KeyDown to track key state.</summary>
    public void OnKeyDown(Keys key)
    {
        if (key == Keys.None) return;
        lock (_keysLock) { _eventKeysDown.Add(key); }
    }

    /// <summary>Called from GameWindow.KeyUp to track key state.</summary>
    public void OnKeyUp(Keys key)
    {
        lock (_keysLock) { _eventKeysDown.Remove(key); }
    }

    /// <summary>Clear event-tracked keys (e.g. when window loses focus).</summary>
    public void ClearKeys()
    {
        lock (_keysLock) { _eventKeysDown.Clear(); }
    }

    public Point MousePosition => Mouse.Position;
    public bool MouseLeftDown => Mouse.LeftButton == ButtonState.Pressed;
    public bool MouseRightDown => Mouse.RightButton == ButtonState.Pressed;
    public bool MouseMiddleDown => Mouse.MiddleButton == ButtonState.Pressed;

    public bool MouseLeftPressed => Mouse.LeftButton == ButtonState.Pressed && _mousePrev.LeftButton == ButtonState.Released;
    public bool MouseRightPressed => Mouse.RightButton == ButtonState.Pressed && _mousePrev.RightButton == ButtonState.Released;
    public bool MouseMiddlePressed => Mouse.MiddleButton == ButtonState.Pressed && _mousePrev.MiddleButton == ButtonState.Released;

    public bool MouseLeftReleased => Mouse.LeftButton == ButtonState.Released && _mousePrev.LeftButton == ButtonState.Pressed;
    public bool MouseRightReleased => Mouse.RightButton == ButtonState.Released && _mousePrev.RightButton == ButtonState.Pressed;
    public bool MouseMiddleReleased => Mouse.MiddleButton == ButtonState.Released && _mousePrev.MiddleButton == ButtonState.Pressed;

    public int ScrollWheelDelta => Mouse.ScrollWheelValue - _mousePrev.ScrollWheelValue;

    public bool IsKeyDown(Keys key) => Keyboard.IsKeyDown(key);
    public bool IsKeyPressed(Keys key) => Keyboard.IsKeyDown(key) && !_keyPrev.IsKeyDown(key);
    public bool IsKeyReleased(Keys key) => !Keyboard.IsKeyDown(key) && _keyPrev.IsKeyDown(key);

    /// <summary>True while left or middle button is held after initial press (for drag).</summary>
    public bool IsDragging => _dragStart.HasValue && (MouseLeftDown || MouseMiddleDown);

    private Point? _dragStart;
    public Point? DragStart => _dragStart;
    public Point DragDelta => _dragStart.HasValue ? new Point(MousePosition.X - _dragStart.Value.X, MousePosition.Y - _dragStart.Value.Y) : Point.Zero;

    /// <summary>Update input state. Pass true when the game window is the active window (e.g. IsActive).</summary>
    public void Update(bool gameWindowActive = true)
    {
        _mousePrev = Mouse;
        _keyPrev = Keyboard;
        Mouse = Microsoft.Xna.Framework.Input.Mouse.GetState();

        // Merge: MonoGame polling + KeyDown/KeyUp events + Windows GetAsyncKeyState when active (fallback when MonoGame input fails)
        var polled = Microsoft.Xna.Framework.Input.Keyboard.GetState();
        var merged = new HashSet<Keys>();
        foreach (var k in polled.GetPressedKeys())
            merged.Add(k);
        lock (_keysLock)
        {
            foreach (var k in _eventKeysDown)
                merged.Add(k);
        }
        if (gameWindowActive && OperatingSystem.IsWindows())
            MergeWindowsKeyState(merged);
        Keyboard = merged.Count > 0 ? new KeyboardState(merged.ToArray()) : new KeyboardState();

        if (gameWindowActive && OperatingSystem.IsWindows())
            UpdateWin32EditShortcutState();

        if (MouseLeftPressed || MouseMiddlePressed)
            _dragStart = MousePosition;
        if (MouseLeftReleased || MouseMiddleReleased)
            _dragStart = null;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    /// <summary>Reads Ctrl/Shift state via Win32 so shortcuts can be handled in KeyDown before the OS consumes them. Only valid on Windows.</summary>
    public static (bool ctrl, bool shift) GetModifierKeysDown()
    {
        if (!OperatingSystem.IsWindows()) return (false, false);
        const int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3;
        bool ctrl = (GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0 || (GetAsyncKeyState(VK_RCONTROL) & 0x8000) != 0;
        bool shift = (GetAsyncKeyState(VK_LSHIFT) & 0x8000) != 0 || (GetAsyncKeyState(VK_RSHIFT) & 0x8000) != 0;
        return (ctrl, shift);
    }

    private static void MergeWindowsKeyState(HashSet<Keys> into)
    {
        // Virtual key codes (winuser.h); high bit of return = key is down
        int VK_SPACE = 0x20, VK_DELETE = 0x2E, VK_O = 0x4F, VK_R = 0x52, VK_S = 0x53, VK_Z = 0x5A, VK_Y = 0x59;
        int VK_1 = 0x31, VK_2 = 0x32, VK_OEM_4 = 0xDB, VK_OEM_6 = 0xDD;
        int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3;
        if ((GetAsyncKeyState(VK_SPACE) & 0x8000) != 0) into.Add(Keys.Space);
        if ((GetAsyncKeyState(VK_DELETE) & 0x8000) != 0) into.Add(Keys.Delete);
        if ((GetAsyncKeyState(VK_O) & 0x8000) != 0) into.Add(Keys.O);
        if ((GetAsyncKeyState(VK_R) & 0x8000) != 0) into.Add(Keys.R);
        if ((GetAsyncKeyState(VK_S) & 0x8000) != 0) into.Add(Keys.S);
        if ((GetAsyncKeyState(VK_Z) & 0x8000) != 0) into.Add(Keys.Z);
        if ((GetAsyncKeyState(VK_Y) & 0x8000) != 0) into.Add(Keys.Y);
        if ((GetAsyncKeyState(VK_1) & 0x8000) != 0) into.Add(Keys.D1);
        if ((GetAsyncKeyState(VK_2) & 0x8000) != 0) into.Add(Keys.D2);
        int VK_3 = 0x33, VK_4 = 0x34, VK_5 = 0x35, VK_6 = 0x36, VK_7 = 0x37, VK_8 = 0x38, VK_9 = 0x39, VK_0 = 0x30;
        if ((GetAsyncKeyState(VK_3) & 0x8000) != 0) into.Add(Keys.D3);
        if ((GetAsyncKeyState(VK_4) & 0x8000) != 0) into.Add(Keys.D4);
        if ((GetAsyncKeyState(VK_5) & 0x8000) != 0) into.Add(Keys.D5);
        if ((GetAsyncKeyState(VK_6) & 0x8000) != 0) into.Add(Keys.D6);
        if ((GetAsyncKeyState(VK_7) & 0x8000) != 0) into.Add(Keys.D7);
        if ((GetAsyncKeyState(VK_8) & 0x8000) != 0) into.Add(Keys.D8);
        if ((GetAsyncKeyState(VK_9) & 0x8000) != 0) into.Add(Keys.D9);
        if ((GetAsyncKeyState(VK_0) & 0x8000) != 0) into.Add(Keys.D0);
        int VK_C = 0x43, VK_X = 0x58, VK_V = 0x56;
        if ((GetAsyncKeyState(VK_C) & 0x8000) != 0) into.Add(Keys.C);
        if ((GetAsyncKeyState(VK_X) & 0x8000) != 0) into.Add(Keys.X);
        if ((GetAsyncKeyState(VK_V) & 0x8000) != 0) into.Add(Keys.V);
        if ((GetAsyncKeyState(VK_OEM_4) & 0x8000) != 0) into.Add(Keys.OemOpenBrackets);
        if ((GetAsyncKeyState(VK_OEM_6) & 0x8000) != 0) into.Add(Keys.OemCloseBrackets);
        if ((GetAsyncKeyState(VK_LSHIFT) & 0x8000) != 0) into.Add(Keys.LeftShift);
        if ((GetAsyncKeyState(VK_RSHIFT) & 0x8000) != 0) into.Add(Keys.RightShift);
        if ((GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0) into.Add(Keys.LeftControl);
        if ((GetAsyncKeyState(VK_RCONTROL) & 0x8000) != 0) into.Add(Keys.RightControl);
        // WASD / QE / C for game view
        if ((GetAsyncKeyState(0x57) & 0x8000) != 0) into.Add(Keys.W);
        if ((GetAsyncKeyState(0x41) & 0x8000) != 0) into.Add(Keys.A);
        if ((GetAsyncKeyState(0x53) & 0x8000) != 0) into.Add(Keys.S);
        if ((GetAsyncKeyState(0x44) & 0x8000) != 0) into.Add(Keys.D);
        if ((GetAsyncKeyState(0x51) & 0x8000) != 0) into.Add(Keys.Q);
        if ((GetAsyncKeyState(0x45) & 0x8000) != 0) into.Add(Keys.E);
    }

    /// <summary>If a Ctrl+Z/Y/C/X/V/A transition was detected this frame via Win32 (for when KeyDown/merged state miss it). Cleared after read.</summary>
    public (Keys key, bool shift)? Win32EditShortcutPressed { get; private set; }

    private void UpdateWin32EditShortcutState()
    {
        const int VK_Z = 0x5A, VK_Y = 0x59, VK_C = 0x43, VK_X = 0x58, VK_V = 0x56, VK_A = 0x41;
        const int VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1;
        bool ctrl = (GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0 || (GetAsyncKeyState(VK_RCONTROL) & 0x8000) != 0;
        bool shift = (GetAsyncKeyState(VK_LSHIFT) & 0x8000) != 0 || (GetAsyncKeyState(VK_RSHIFT) & 0x8000) != 0;
        bool z = (GetAsyncKeyState(VK_Z) & 0x8000) != 0;
        bool y = (GetAsyncKeyState(VK_Y) & 0x8000) != 0;
        bool c = (GetAsyncKeyState(VK_C) & 0x8000) != 0;
        bool x = (GetAsyncKeyState(VK_X) & 0x8000) != 0;
        bool v = (GetAsyncKeyState(VK_V) & 0x8000) != 0;
        bool a = (GetAsyncKeyState(VK_A) & 0x8000) != 0;

        Win32EditShortcutPressed = null;
        if (ctrl)
        {
            if (z && !_win32PrevZ) Win32EditShortcutPressed = (Keys.Z, shift);
            else if (y && !_win32PrevY) Win32EditShortcutPressed = (Keys.Y, shift);
            else if (c && !_win32PrevC) Win32EditShortcutPressed = (Keys.C, shift);
            else if (x && !_win32PrevX) Win32EditShortcutPressed = (Keys.X, shift);
            else if (v && !_win32PrevV) Win32EditShortcutPressed = (Keys.V, shift);
            else if (a && !_win32PrevA) Win32EditShortcutPressed = (Keys.A, shift);
        }
        _win32PrevZ = z;
        _win32PrevY = y;
        _win32PrevC = c;
        _win32PrevX = x;
        _win32PrevV = v;
        _win32PrevA = a;
    }
}
