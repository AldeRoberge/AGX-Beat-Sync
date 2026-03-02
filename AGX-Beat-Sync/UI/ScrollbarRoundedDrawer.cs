using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.UI;

/// <summary>Draws scrollbar track and thumb with rounded corners. Uses 9-slice so corners stay circular; texture has the draw color baked in so it doesn't appear lighter from tint blending.</summary>
public static class ScrollbarRoundedDrawer
{
    private const int TextureSize = 32;
    private const int TextureCornerRadius = 8;
    private const int DisplayCornerRadius = 4;

    private static GraphicsDevice? s_device;
    private static readonly Dictionary<int, Texture2D> s_texturesByColor = new();

    private static int ColorKey(Color c) => c.R | (c.G << 8) | (c.B << 16);

    private static float GetRoundedRectCoverage(float px, float py, int w, int h, int r)
    {
        if (r <= 0) return (px >= 0 && px < w && py >= 0 && py < h) ? 1f : 0f;
        float cx = px + 0.5f;
        float cy = py + 0.5f;
        if (cx >= r && cx < w - r && cy >= r && cy < h - r) return 1f;
        if (cx < r && cy < r)
        {
            float dx = cx - r, dy = cy - r;
            return Math.Clamp((r + 0.5f - MathF.Sqrt(dx * dx + dy * dy)) / 1f, 0f, 1f);
        }
        if (cx >= w - r && cy < r)
        {
            float dx = cx - (w - r), dy = cy - r;
            return Math.Clamp((r + 0.5f - MathF.Sqrt(dx * dx + dy * dy)) / 1f, 0f, 1f);
        }
        if (cx < r && cy >= h - r)
        {
            float dx = cx - r, dy = cy - (h - r);
            return Math.Clamp((r + 0.5f - MathF.Sqrt(dx * dx + dy * dy)) / 1f, 0f, 1f);
        }
        if (cx >= w - r && cy >= h - r)
        {
            float dx = cx - (w - r), dy = cy - (h - r);
            return Math.Clamp((r + 0.5f - MathF.Sqrt(dx * dx + dy * dy)) / 1f, 0f, 1f);
        }
        return 1f;
    }

    private static Texture2D CreateRoundedRectSolid(GraphicsDevice gd, int w, int h, int r, Color fill)
    {
        var data = new Color[w * h];
        for (int py = 0; py < h; py++)
        {
            for (int px = 0; px < w; px++)
            {
                float coverage = GetRoundedRectCoverage(px, py, w, h, r);
                byte a = (byte)(Math.Clamp(coverage, 0f, 1f) * 255f);
                data[py * w + px] = new Color(fill.R, fill.G, fill.B, a);
            }
        }
        var tex = new Texture2D(gd, w, h);
        tex.SetData(data);
        return tex;
    }

    private static Texture2D GetOrCreateTexture(GraphicsDevice gd, Color color)
    {
        if (s_device != gd)
        {
            foreach (var t in s_texturesByColor.Values)
                t?.Dispose();
            s_texturesByColor.Clear();
            s_device = gd;
        }
        int key = ColorKey(color);
        if (s_texturesByColor.TryGetValue(key, out var existing))
            return existing;
        var tex = CreateRoundedRectSolid(gd, TextureSize, TextureSize, TextureCornerRadius, color);
        s_texturesByColor[key] = tex;
        return tex;
    }

    private static void DrawRoundedRect9Slice(SpriteBatch sb, Texture2D tex, int x, int y, int w, int h,
        int displayRadius, int textureRadius, int texW, int texH)
    {
        int tr = textureRadius;
        int tr2 = tr * 2;
        int er = Math.Min(displayRadius, Math.Min(Math.Max(0, (w - 1) / 2), Math.Max(0, (h - 1) / 2)));
        if (er <= 0)
        {
            sb.Draw(tex, new Rectangle(x, y, Math.Max(1, w), Math.Max(1, h)), new Rectangle(0, 0, texW, texH), Color.White);
            return;
        }
        int leftW = er, rightW = er, centerW = w - leftW - rightW;
        int topH = er, bottomH = er, centerH = h - topH - bottomH;
        int srcL = 0, srcR = tr, srcCw = texW - tr2, srcRx = texW - tr;
        int srcT = 0, srcM = tr, srcCh = texH - tr2, srcBy = texH - tr;
        sb.Draw(tex, new Rectangle(x, y, leftW, topH), new Rectangle(srcL, srcT, tr, tr), Color.White);
        if (centerW > 0) sb.Draw(tex, new Rectangle(x + leftW, y, centerW, topH), new Rectangle(srcR, srcT, srcCw, tr), Color.White);
        sb.Draw(tex, new Rectangle(x + w - rightW, y, rightW, topH), new Rectangle(srcRx, srcT, tr, tr), Color.White);
        if (centerH > 0)
        {
            sb.Draw(tex, new Rectangle(x, y + topH, leftW, centerH), new Rectangle(srcL, srcM, tr, srcCh), Color.White);
            if (centerW > 0) sb.Draw(tex, new Rectangle(x + leftW, y + topH, centerW, centerH), new Rectangle(srcR, srcM, srcCw, srcCh), Color.White);
            sb.Draw(tex, new Rectangle(x + w - rightW, y + topH, rightW, centerH), new Rectangle(srcRx, srcM, tr, srcCh), Color.White);
        }
        sb.Draw(tex, new Rectangle(x, y + h - bottomH, leftW, bottomH), new Rectangle(srcL, srcBy, tr, tr), Color.White);
        if (centerW > 0) sb.Draw(tex, new Rectangle(x + leftW, y + h - bottomH, centerW, bottomH), new Rectangle(srcR, srcBy, srcCw, tr), Color.White);
        sb.Draw(tex, new Rectangle(x + w - rightW, y + h - bottomH, rightW, bottomH), new Rectangle(srcRx, srcBy, tr, tr), Color.White);
    }

    /// <summary>Draws a rounded rectangle (scrollbar track or thumb). Color is baked into the texture so it matches exactly; 9-slice keeps corners circular.</summary>
    public static void DrawRoundedScrollbar(SpriteBatch sb, GraphicsDevice gd, Rectangle rect, Color color)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        var tex = GetOrCreateTexture(gd, color);
        DrawRoundedRect9Slice(sb, tex, rect.X, rect.Y, rect.Width, rect.Height,
            DisplayCornerRadius, TextureCornerRadius, TextureSize, TextureSize);
    }

    /// <summary>Draws the scrollbar track (background). Fills the rect with the track color first so the rounded track's transparent corners show the same color instead of the panel background (no square/lighter area).</summary>
    public static void DrawRoundedScrollbarTrack(SpriteBatch sb, GraphicsDevice gd, Rectangle rect, Color color, Texture2D pixel)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        sb.Draw(pixel, rect, color);
        DrawRoundedScrollbar(sb, gd, rect, color);
    }
}
