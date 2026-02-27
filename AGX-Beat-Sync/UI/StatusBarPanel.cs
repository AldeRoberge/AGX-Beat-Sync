using AGX_Beat_Sync.Editor;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.UI;

/// <summary>
/// Bottom bar that shows hover description for the control under the mouse.
/// </summary>
public class StatusBarPanel : PanelBase
{
    private const int TextPadding = 8;

    /// <summary>Text to display (e.g. "Timeline • Track: Spawn Entity • 1.5s"). Set by the game from panel hover hit-test.</summary>
    public string HoverText { get; set; } = "";

    public StatusBarPanel()
    {
        Title = "";
        HeaderHeight = 0;
        BackgroundColor = new Color(35, 37, 41);
        BorderColor = new Color(55, 58, 65);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);
        spriteBatch.Draw(pixel, Bounds, BackgroundColor);
        // Top border only
        spriteBatch.Draw(pixel, new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, 1), BorderColor);
        DrawContent(spriteBatch);
    }

    /// <summary>Typical line height for 11pt Segoe UI. Nudge up so descenders aren't clipped at bottom.</summary>
    private const int LabelLineHeight = 16;
    private const int BottomNudge = 2;

    protected override void DrawContent(SpriteBatch spriteBatch)
    {
        if (string.IsNullOrEmpty(HoverText))
            return;
        var device = spriteBatch.GraphicsDevice;
        int x = Bounds.X + TextPadding;
        int y = Bounds.Y + (Bounds.Height - LabelLineHeight) / 2 - BottomNudge;
        InspectorDrawer.DrawLabel(spriteBatch, device, x, y, HoverText, GetPixelTexture(device));
    }
}
