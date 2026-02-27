using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.UI;

/// <summary>
/// Base for all editor panels. Provides bounds, background, and optional header.
/// </summary>
public abstract class PanelBase
{
    private static Texture2D? s_pixel;

    public static Texture2D GetPixelTexture(GraphicsDevice device)
    {
        if (s_pixel == null || s_pixel.GraphicsDevice != device)
        {
            s_pixel?.Dispose();
            s_pixel = new Texture2D(device, 1, 1);
            s_pixel.SetData(new[] { Color.White });
        }
        return s_pixel;
    }

    public Rectangle Bounds { get; set; }
    public string Title { get; set; } = "Panel";
    public Color BackgroundColor { get; set; }
    public Color HeaderColor { get; set; }
    public Color BorderColor { get; set; }
    public int HeaderHeight { get; set; } = 24;

    protected PanelBase()
    {
        BackgroundColor = new Color(40, 42, 46);
        HeaderColor = new Color(55, 58, 64);
        BorderColor = new Color(70, 73, 80);
    }

    public Rectangle HeaderBounds => new(Bounds.X, Bounds.Y, Bounds.Width, HeaderHeight);
    public virtual Rectangle ContentBounds => new(Bounds.X, Bounds.Y + HeaderHeight, Bounds.Width, Bounds.Height - HeaderHeight);

    public virtual void Update(GameTime gameTime) { }

    public virtual void Draw(SpriteBatch spriteBatch)
    {
        DrawPanelBackground(spriteBatch);
        DrawContent(spriteBatch);
    }

    protected virtual void DrawPanelBackground(SpriteBatch spriteBatch)
    {
        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);
        spriteBatch.Draw(pixel, Bounds, BackgroundColor);
        spriteBatch.Draw(pixel, HeaderBounds, HeaderColor);
        DrawBorder(spriteBatch, pixel);
    }

    protected virtual void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel)
    {
        int t = 1;
        spriteBatch.Draw(pixel, new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, t), BorderColor);
        spriteBatch.Draw(pixel, new Rectangle(Bounds.X, Bounds.Bottom - t, Bounds.Width, t), BorderColor);
        spriteBatch.Draw(pixel, new Rectangle(Bounds.X, Bounds.Y, t, Bounds.Height), BorderColor);
        spriteBatch.Draw(pixel, new Rectangle(Bounds.Right - t, Bounds.Y, t, Bounds.Height), BorderColor);
    }

    /// <summary>Override to draw panel-specific content (inside content area).</summary>
    protected abstract void DrawContent(SpriteBatch spriteBatch);

    /// <summary>Whether the given point is inside this panel's bounds.</summary>
    public bool ContainsPoint(Point point) => Bounds.Contains(point);

    /// <summary>Optional hover description for the given point. Override in panels to show what the cursor is over.</summary>
    public virtual string? GetHoverText(Point mouse) => null;

    /// <summary>Optional cursor to show when the mouse is over this panel at the given point. Return null for default arrow.</summary>
    public virtual MouseCursor? GetDesiredCursor(Point mouse) => null;
}
