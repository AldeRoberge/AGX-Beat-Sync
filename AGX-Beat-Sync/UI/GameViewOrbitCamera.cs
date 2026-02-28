using System;
using AGX_Beat_Sync.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.UI;

/// <summary>Orbit camera that follows a target. Middle-drag: full 3D orbit. Right-drag: FPS (WASD + look). Q/E: horizontal orbit when not dragging.</summary>
public sealed class GameViewOrbitCamera
{
    private const float OrbitSpeed = 2.5f;
    private const float FpsMoveSpeed = 12f;
    private const float MouseLookSensitivity = 0.0035f;
    private const float OrbitDragSensitivity = 0.004f;
    private const float MinPitch = -1.4f;
    private const float MaxPitch = -0.2f;
    private const float MinDistance = 1f;
    private const float MaxDistance = 200f;
    private const float DefaultDistance = 8f;
    private const float ZoomSensitivity = 0.0008f;
    private const float DefaultPitch = -0.644f;
    private const float DefaultYaw = -0.26f;

    /// <summary>World position the camera looks at (e.g. player).</summary>
    public Vector3 Target { get; set; }

    /// <summary>Orbit angle around target (radians). Q/E and right-drag modify this.</summary>
    public float OrbitYaw { get; private set; } = DefaultYaw;

    /// <summary>Pitch (radians). Right-drag modifies this.</summary>
    public float OrbitPitch { get; private set; } = DefaultPitch;

    /// <summary>Distance from target.</summary>
    public float Distance { get; private set; } = DefaultDistance;

    /// <summary>Camera position in world space.</summary>
    public Vector3 Position { get; private set; }

    /// <summary>Restore orbit state from saved session (yaw/pitch in radians, distance clamped).</summary>
    public void SetOrbitState(float yaw, float pitch, float distance)
    {
        OrbitYaw = yaw;
        OrbitPitch = Math.Clamp(pitch, MinPitch, MaxPitch);
        Distance = Math.Clamp(distance, MinDistance, MaxDistance);
    }

    public bool IsCapturingMouse => _capturing;

    private bool _capturing;
    private bool _orbitCapturing;
    private Point _lastMousePos;
    /// <summary>When true, camera uses manual FPS position instead of orbit.</summary>
    private Vector3 _fpsPosition;

    /// <summary>Builds view matrix. When capturing uses FPS position; otherwise orbit around Target.</summary>
    public Matrix GetViewMatrix()
    {
        if (_capturing)
            Position = _fpsPosition;
        else
            Position = ComputePosition();
        return Matrix.CreateLookAt(Position, Target, Vector3.Up);
    }

    /// <summary>Handles middle-drag (full 3D orbit), right-drag (FPS look + WASD), Q/E orbit when not dragging. Call when game view has focus.</summary>
    public void HandleInput(InputManager? input, Rectangle viewportRect, float dt)
    {
        if (input == null)
            return;

        bool mouseInViewport = viewportRect.Contains(input.MousePosition);

        // Middle-drag: full 3D orbit around target (player)
        if (mouseInViewport && input.MouseMiddlePressed)
        {
            _orbitCapturing = true;
            _lastMousePos = input.MousePosition;
        }
        if (input.MouseMiddleReleased)
            _orbitCapturing = false;

        if (_orbitCapturing)
        {
            var delta = new Point(input.MousePosition.X - _lastMousePos.X, input.MousePosition.Y - _lastMousePos.Y);
            OrbitYaw -= delta.X * OrbitDragSensitivity;
            OrbitPitch -= delta.Y * OrbitDragSensitivity;
            OrbitPitch = Math.Clamp(OrbitPitch, MinPitch, MaxPitch);
            _lastMousePos = input.MousePosition;
        }

        if (mouseInViewport && input.MouseRightPressed)
        {
            _capturing = true;
            _fpsPosition = Position; // use current position as FPS start
            _lastMousePos = input.MousePosition;
        }
        if (input.MouseRightReleased)
        {
            if (_capturing)
                SyncOrbitFromPosition(); // so next frame orbit matches current view
            _capturing = false;
        }

        if (_capturing)
        {
            // FPS look
            var delta = new Point(input.MousePosition.X - _lastMousePos.X, input.MousePosition.Y - _lastMousePos.Y);
            OrbitYaw -= delta.X * MouseLookSensitivity;
            OrbitPitch -= delta.Y * MouseLookSensitivity;
            OrbitPitch = Math.Clamp(OrbitPitch, MinPitch, MaxPitch);
            _lastMousePos = input.MousePosition;
            int cx = viewportRect.X + viewportRect.Width / 2;
            int cy = viewportRect.Y + viewportRect.Height / 2;
            if (viewportRect.Width > 0 && viewportRect.Height > 0)
                Mouse.SetPosition(cx, cy);
            _lastMousePos = new Point(cx, cy);

            // FPS WASD: move camera and target together
            var (forward, right) = GetFpsForwardRight();
            Vector3 move = Vector3.Zero;
            if (input.IsKeyDown(Keys.W)) move += forward;
            if (input.IsKeyDown(Keys.S)) move -= forward;
            if (input.IsKeyDown(Keys.A)) move -= right;
            if (input.IsKeyDown(Keys.D)) move += right;
            if (input.IsKeyDown(Keys.Space)) move += Vector3.Up;
            if (input.IsKeyDown(Keys.C)) move -= Vector3.Up;
            if (move != Vector3.Zero)
            {
                move.Normalize();
                float step = FpsMoveSpeed * dt;
                _fpsPosition += move * step;
                Target += move * step;
            }
        }
        else if (mouseInViewport && !_orbitCapturing)
        {
            if (input.IsKeyDown(Keys.Q)) OrbitYaw += OrbitSpeed * dt;
            if (input.IsKeyDown(Keys.E)) OrbitYaw -= OrbitSpeed * dt;

            // Scroll wheel: zoom in (decrease distance) / zoom out (increase distance)
            int scroll = input.ScrollWheelDelta;
            if (scroll != 0)
            {
                float factor = 1f - scroll * ZoomSensitivity;
                Distance = Math.Clamp(Distance * factor, MinDistance, MaxDistance);
            }
        }
    }

    /// <summary>Forward (XZ) and right (XZ) for camera-relative movement. W = forward, D = right (screen-right).</summary>
    public (Vector3 forwardXZ, Vector3 rightXZ) GetCameraForwardRightXZ()
    {
        var forward = Target - Position;
        forward.Y = 0;
        if (forward.LengthSquared() < 0.0001f)
            forward = -Vector3.UnitZ;
        forward.Normalize();
        var right = Vector3.Cross(Vector3.Up, forward);
        right.Normalize();
        // Negate so D = +right moves character to viewer's right (correct handedness for camera-relative)
        return (forward, -right);
    }

    private (Vector3 forward, Vector3 right) GetFpsForwardRight()
    {
        var rot = Matrix.CreateRotationX(OrbitPitch) * Matrix.CreateRotationY(OrbitYaw);
        var forward = Vector3.Transform(-Vector3.UnitZ, rot);
        forward.Y = 0;
        if (forward.LengthSquared() > 0.0001f) forward.Normalize();
        var right = Vector3.Cross(forward, Vector3.Up);
        return (forward, right);
    }

    private void SyncOrbitFromPosition()
    {
        var dir = Target - Position;
        float d = dir.Length();
        if (d < 0.0001f) return;
        dir /= d;
        Distance = d;
        OrbitPitch = MathF.Asin(Math.Clamp(dir.Y, -1f, 1f));
        OrbitYaw = MathF.Atan2(dir.X, dir.Z);
    }

    private Vector3 ComputePosition()
    {
        float x = MathF.Cos(OrbitPitch) * MathF.Sin(OrbitYaw);
        float y = MathF.Sin(OrbitPitch);
        float z = MathF.Cos(OrbitPitch) * MathF.Cos(OrbitYaw);
        var offset = new Vector3(x, y, z) * -Distance;
        return Target + offset;
    }
}
