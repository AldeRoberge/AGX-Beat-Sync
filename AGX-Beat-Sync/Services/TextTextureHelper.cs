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
    /// <summary>Renders the given text to a new Texture2D. Returns null on failure.</summary>
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
                g.Clear(System.Drawing.Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.DrawString(text, font, System.Drawing.Brushes.White, 0, 0);
            }
            return BitmapToTexture(device, bitmap, width, height);
        }
        catch
        {
            return null;
        }
    }

    private static Texture2D? BitmapToTexture(GraphicsDevice device, Bitmap bitmap, int width, int height)
    {
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
                    int i = y * width + x;
                    int off = y * bmpData.Stride + x * 4;
                    data[i] = new Color(rawBytes[off + 2], rawBytes[off + 1], rawBytes[off], rawBytes[off + 3]);
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
