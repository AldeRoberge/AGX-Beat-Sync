using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.Services;

/// <summary>
/// Creates MonoGame Texture2D from text using system fonts (e.g. for overlays and dialogs).
/// </summary>
public static class TextTextureHelper
{
    private static readonly System.Drawing.Color ChromaKey = System.Drawing.Color.Magenta; // (255, 0, 255) – never appears in black text

    /// <summary>Renders the given text to a new Texture2D. Returns null on failure.</summary>
    /// <remarks>
    /// Draws black text on a magenta chroma-key background, then converts to white text with alpha
    /// so the background is fully transparent and GDI+ anti-aliasing works.
    /// </remarks>
    public static Texture2D? Create(GraphicsDevice device, string text, string fontName = "Segoe UI", int fontSize = 20)
    {
        if (string.IsNullOrEmpty(text)) return null;
        try
        {
            using var font = new Font(fontName, fontSize, FontStyle.Regular);
            int width = 512;
            int height = 64;
            using var bitmap = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(ChromaKey);
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.DrawString(text, font, System.Drawing.Brushes.Black, 0, 0);
            }
            return BitmapChromaKeyToWhiteAlpha(device, bitmap, width, height);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Converts black-on-chroma bitmap to white text; chroma and near-white pixels become fully transparent.</summary>
    private static Texture2D? BitmapChromaKeyToWhiteAlpha(GraphicsDevice device, Bitmap bitmap, int width, int height)
    {
        // Only treat pure magenta as chroma (don't kill ClearType/anti-aliasing edges).
        const int LuminanceBackgroundThreshold = 252; // Only near-pure-white = background (avoids killing text edges).
        var data = new Color[width * height];
        var rect = new System.Drawing.Rectangle(0, 0, width, height);
        var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int byteCount = Math.Abs(bmpData.Stride) * height;
            var rawBytes = new byte[byteCount];
            Marshal.Copy(bmpData.Scan0, rawBytes, 0, byteCount);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int off = y * bmpData.Stride + x * 4;
                    byte b = rawBytes[off], g = rawBytes[off + 1], r = rawBytes[off + 2];
                    int lum = (r + g + b) / 3;
                    bool isChroma = g <= 40 && r >= 200 && b >= 200; // strict: magenta only
                    bool isBackground = lum >= LuminanceBackgroundThreshold;
                    byte alpha = (isChroma || isBackground) ? (byte)0 : (byte)(255 - (byte)lum);
                    int i = y * width + x;
                    data[i] = new Color((byte)255, (byte)255, (byte)255, alpha);
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }
        var tex = new Texture2D(device, width, height);
        tex.SetData(data);
        return tex;
    }
}
