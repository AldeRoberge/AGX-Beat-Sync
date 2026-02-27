using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.Editor;

public static class TimelinePlayheadRenderer
{
    /// <summary>FL Studio-style bright playhead line.</summary>
    private static readonly Color PlayheadColor = new(255, 70, 70);

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Rectangle contentBounds, float screenX)
    {
        if (screenX < contentBounds.X - 1 || screenX > contentBounds.Right + 1)
            return;
        spriteBatch.Draw(pixel, new Rectangle((int)screenX, contentBounds.Y, 2, contentBounds.Height), PlayheadColor);
    }
}
