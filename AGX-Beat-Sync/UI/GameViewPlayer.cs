using AGX_Beat_Sync.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.UI;

/// <summary>Player in the game view. Four-frame sprite: 0=right, 1=down, 2=left, 3=up.</summary>
public sealed class GameViewPlayer
{
    public const int FrameCount = 4;
    public const float MoveSpeed = 8f;
    /// <summary>World Y for the player (ground plane). 0 = feet on the ground.</summary>
    public const float Height = 0f;

    /// <summary>World position (XZ = ground plane, Y = Height).</summary>
    public Vector3 Position { get; private set; }

    /// <summary>Facing frame index: 0=right (+X), 1=down (+Z), 2=left (-X), 3=up (-Z).</summary>
    public int Facing { get; private set; }

    /// <summary>Initial offset from world origin so the player is not on top of the enemy cube at (0,0,0).</summary>
    private const float InitialOffsetZ = 8f;

    public GameViewPlayer()
    {
        Position = new Vector3(0f, Height, InitialOffsetZ);
        Facing = 1; // down
    }

    /// <summary>Set position (e.g. when camera FPS moves the target). Keeps Y = Height.</summary>
    public void SetPosition(Vector3 worldPosition)
    {
        Position = new Vector3(worldPosition.X, Height, worldPosition.Z);
    }

    /// <summary>Updates position from WASD in camera-relative directions. forwardXZ/rightXZ are unit vectors on XZ plane.</summary>
    public void Update(InputManager? input, Rectangle viewportRect, float dt, Vector3 forwardXZ, Vector3 rightXZ)
    {
        if (input == null || !viewportRect.Contains(input.MousePosition))
            return;

        Vector3 move = Vector3.Zero;
        if (input.IsKeyDown(Keys.W)) move += forwardXZ;
        if (input.IsKeyDown(Keys.S)) move -= forwardXZ;
        if (input.IsKeyDown(Keys.A)) move -= rightXZ;
        if (input.IsKeyDown(Keys.D)) move += rightXZ;

        if (move != Vector3.Zero)
        {
            move.Normalize();
            Position += move * MoveSpeed * dt;
            Position = new Vector3(Position.X, Height, Position.Z);
            Facing = FacingFromMove(move);
        }
    }

    /// <summary>Source rectangle for the current frame. Assumes 4 frames in a horizontal strip.</summary>
    public Rectangle GetSourceRectangle(Texture2D texture)
    {
        return GetSourceRectangleForFrame(texture, Facing);
    }

    /// <summary>Source rectangle for a specific frame index (0=right, 1=down, 2=left, 3=up).</summary>
    public static Rectangle GetSourceRectangleForFrame(Texture2D texture, int frame)
    {
        int w = texture.Width / FrameCount;
        int x = (frame % FrameCount) * w;
        return new Rectangle(x, 0, w, texture.Height);
    }

    /// <summary>Unit facing direction on the XZ plane (Y=0). 0=+X, 1=+Z, 2=-X, 3=-Z.</summary>
    public Vector3 GetFacingDirectionXZ()
    {
        return Facing switch
        {
            0 => Vector3.UnitX,
            1 => Vector3.UnitZ,
            2 => -Vector3.UnitX,
            3 => -Vector3.UnitZ,
            _ => -Vector3.UnitZ
        };
    }

    private static int FacingFromMove(Vector3 move)
    {
        float ax = Math.Abs(move.X);
        float az = Math.Abs(move.Z);
        if (ax >= az)
            // Swap 0/2 so sprite matches camera-relative "right" (rightXZ is negated in camera)
            return move.X > 0 ? 2 : 0; // world +X → show left(2); world -X → show right(0)
        // Swap 1/3 so sprite matches camera-relative forward (same handedness as left/right)
        return move.Z > 0 ? 3 : 1;     // world +Z → up(3); world -Z → down(1)
    }
}
