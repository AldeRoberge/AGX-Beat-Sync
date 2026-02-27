using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.Input;

/// <summary>
/// Centralized input: mouse, keyboard, drag state.
/// Call Update() once per frame at the start of Update.
/// </summary>
public class InputManager
{
    private MouseState _mousePrev;
    private KeyboardState _keyPrev;

    public MouseState Mouse { get; private set; }
    public KeyboardState Keyboard { get; private set; }

    public Point MousePosition => Mouse.Position;
    public bool MouseLeftDown => Mouse.LeftButton == ButtonState.Pressed;
    public bool MouseRightDown => Mouse.RightButton == ButtonState.Pressed;
    public bool MouseMiddleDown => Mouse.MiddleButton == ButtonState.Pressed;

    public bool MouseLeftPressed => Mouse.LeftButton == ButtonState.Pressed && _mousePrev.LeftButton == ButtonState.Released;
    public bool MouseRightPressed => Mouse.RightButton == ButtonState.Pressed && _mousePrev.RightButton == ButtonState.Released;
    public bool MouseMiddlePressed => Mouse.MiddleButton == ButtonState.Pressed && _mousePrev.MiddleButton == ButtonState.Released;

    public bool MouseLeftReleased => Mouse.LeftButton == ButtonState.Released && _mousePrev.LeftButton == ButtonState.Pressed;
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

    public void Update()
    {
        _mousePrev = Mouse;
        _keyPrev = Keyboard;
        Mouse = Microsoft.Xna.Framework.Input.Mouse.GetState();
        Keyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();

        if (MouseLeftPressed || MouseMiddlePressed)
            _dragStart = MousePosition;
        if (MouseLeftReleased || MouseMiddleReleased)
            _dragStart = null;
    }
}
