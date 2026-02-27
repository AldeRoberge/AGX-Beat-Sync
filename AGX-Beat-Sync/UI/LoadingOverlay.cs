using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AGX_Beat_Sync.Services;

namespace AGX_Beat_Sync.UI;

/// <summary>
/// Full-screen overlay: message + progress bar. Used during audio load (and optional BPM detection).
/// </summary>
public static class LoadingOverlay
{
    private static Texture2D? s_textTexture;
    private static string s_cachedMessage = "";
    private static GraphicsDevice? s_cachedDevice;

    private const string DefaultMessage = "Loading audio and building waveform";

    public static LoadingOverlayStyle Style { get; set; } = LoadingOverlayStyle.Default;

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, GraphicsDevice device, double progress, GameTime? gameTime = null)
    {
        int w = device.Viewport.Width;
        int h = device.Viewport.Height;

        spriteBatch.Draw(pixel, new Rectangle(0, 0, w, h), Style.OverlayBackground);
        DrawMessage(spriteBatch, device, w, h);
        DrawProgressBar(spriteBatch, pixel, w, h, progress);
    }

    private static void DrawMessage(SpriteBatch spriteBatch, GraphicsDevice device, int viewportW, int viewportH)
    {
        EnsureTextTexture(device, Style.Message);
        if (s_textTexture == null) return;
        int tx = (viewportW - s_textTexture.Width) / 2;
        int ty = (viewportH - s_textTexture.Height) / 2 - 40;
        spriteBatch.Draw(s_textTexture, new Rectangle(tx, ty, s_textTexture.Width, s_textTexture.Height), Color.White);
    }

    private static void DrawProgressBar(SpriteBatch spriteBatch, Texture2D pixel, int viewportW, int viewportH, double progress)
    {
        int barW = Style.BarWidth;
        int barH = Style.BarHeight;
        int barX = (viewportW - barW) / 2;
        int barY = viewportH / 2 + 10;
        spriteBatch.Draw(pixel, new Rectangle(barX, barY, barW, barH), Style.BarTrack);
        double p = Math.Clamp(progress, 0, 1);
        int fillW = (int)(barW * p);
        if (fillW > 0)
            spriteBatch.Draw(pixel, new Rectangle(barX, barY, fillW, barH), Style.BarFill);
    }

    private static void EnsureTextTexture(GraphicsDevice device, string message)
    {
        if (s_cachedDevice == device && s_textTexture != null && s_cachedMessage == message)
            return;
        s_cachedDevice = device;
        s_cachedMessage = message;
        s_textTexture?.Dispose();
        s_textTexture = TextTextureHelper.Create(device, message, "Segoe UI", 20);
    }

    public struct LoadingOverlayStyle
    {
        public string Message;
        public Color OverlayBackground;
        public Color BarTrack;
        public Color BarFill;
        public int BarWidth;
        public int BarHeight;

        public static LoadingOverlayStyle Default => new()
        {
            Message = DefaultMessage,
            OverlayBackground = new Color(0, 0, 0, 180),
            BarTrack = new Color(48, 52, 58),
            BarFill = new Color(72, 165, 130),
            BarWidth = 400,
            BarHeight = 20
        };
    }
}
