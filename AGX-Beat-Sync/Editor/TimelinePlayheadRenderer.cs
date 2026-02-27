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
    public const int PlayheadBulbRadius = 7;

    /// <summary>Pixels above the content top where the playhead head (bulb) center is drawn; used for hit-testing.</summary>
    public const int PlayheadHeadOffset = 14;

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Rectangle contentBounds, float screenX)
    {
        if (screenX < contentBounds.X - 1 || screenX > contentBounds.Right + 1)
            return;
        int cx = (int)screenX;
        // Bulb centered above the content (easier to grab and drag)
        int bulbCenterY = contentBounds.Y - PlayheadHeadOffset;
        int r = PlayheadBulbRadius;
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            if (dx * dx + dy * dy <= r * r)
                spriteBatch.Draw(pixel, new Rectangle(cx + dx, bulbCenterY + dy, 1, 1), PlayheadBulbColor);
        }
        // Line from below bulb to bottom of content
        int lineTop = bulbCenterY + r;
        if (lineTop < contentBounds.Bottom)
            spriteBatch.Draw(pixel, new Rectangle(cx, lineTop, 2, contentBounds.Bottom - lineTop), PlayheadColor);
    }
}
