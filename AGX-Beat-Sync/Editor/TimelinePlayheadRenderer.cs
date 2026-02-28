using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.Editor;

public static class TimelinePlayheadRenderer
{
    /// <summary>FL Studio-style bright playhead line.</summary>
    private static readonly Color PlayheadColor = new(255, 70, 70);
    /// <summary>Brighter red for the draggable bulb at the top.</summary>
    private static readonly Color PlayheadBulbColor = new(255, 90, 90);

    /// <summary>Radius of the playhead bulb in pixels; used for hit-testing in TimelinePanel.</summary>
    public const int PlayheadBulbRadius = 4;

    /// <summary>Pixels above the content top where the playhead head (bulb) center is drawn; used for hit-testing.</summary>
    public const int PlayheadHeadOffset = 6;

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Rectangle contentBounds, float screenX)
    {
        if (screenX < contentBounds.X - 1 || screenX > contentBounds.Right + 1)
            return;
        int cx = (int)screenX;
        // Bulb centered above the content (soft-edged for less pixelation)
        int bulbCenterY = contentBounds.Y - PlayheadHeadOffset;
        int r = PlayheadBulbRadius;
        float rSq = (r + 0.6f) * (r + 0.6f);
        float rInnerSq = (r - 0.5f) * (r - 0.5f);
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            float distSq = dx * dx + dy * dy;
            if (distSq > rSq) continue;
            byte alpha = 255;
            if (distSq > rInnerSq)
            {
                float dist = MathF.Sqrt(distSq);
                float t = (r + 0.6f - dist) / 1.1f;
                alpha = (byte)(Math.Clamp(t, 0, 1) * 255);
            }
            var color = new Color(PlayheadBulbColor.R, PlayheadBulbColor.G, PlayheadBulbColor.B, alpha);
            spriteBatch.Draw(pixel, new Rectangle(cx + dx, bulbCenterY + dy, 1, 1), color);
        }
        // Line from below bulb to bottom of content (1px for slimmer look)
        int lineTop = bulbCenterY + r;
        if (lineTop < contentBounds.Bottom)
            spriteBatch.Draw(pixel, new Rectangle(cx, lineTop, 1, contentBounds.Bottom - lineTop), PlayheadColor);
    }
}
