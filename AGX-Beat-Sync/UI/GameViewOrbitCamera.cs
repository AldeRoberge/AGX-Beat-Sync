using System;
using AGX_Beat_Sync.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.UI;

/// <summary>Orbit camera that follows a target. Middle-drag: full 3D orbit. Q/E: horizontal orbit when not dragging.</summary>
public sealed class GameViewOrbitCamera
{
    private const float OrbitSpeed = 2.5f;
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

    /// <summary>Orbit angle around target (radians). Q/E and middle-drag modify this.</summary>
    public float OrbitYaw { get; private set; } = DefaultYaw;

    /// <summary>Pitch (radians). Middle-drag modifies this.</summary>
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

    public bool IsCapturingMouse => false;

    private bool _orbitCapturing;
    private Point _lastMousePos;

    /// <summary>Builds view matrix from orbit around Target.</summary>
    public Matrix GetViewMatrix()
    {
        Position = ComputePosition();
        return Matrix.CreateLookAt(Position, Target, Vector3.Up);
    }

    /// <summary>Handles middle-drag (full 3D orbit), Q/E orbit when not dragging. Call when game view has focus.</summary>
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

        if (mouseInViewport && !_orbitCapturing)
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

    private Vector3 ComputePosition()
    {
        float x = MathF.Cos(OrbitPitch) * MathF.Sin(OrbitYaw);
        float y = MathF.Sin(OrbitPitch);
        float z = MathF.Cos(OrbitPitch) * MathF.Cos(OrbitYaw);
        var offset = new Vector3(x, y, z) * -Distance;
        return Target + offset;
    }
}
