using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using AGX_Beat_Sync.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.Editor;

/// <summary>
/// Unity-style inspector drawing: foldouts, labels, enum dropdowns, float/vector fields. Shared by all track inspector renderers.
/// </summary>
public static class InspectorDrawer
{
    public const int RowHeight = 20;
    public const int Indent = 14;
    public const int LabelWidth = 100;
    public const int Padding = 10;
    public const int FontSize = 11;

    private static readonly Dictionary<string, (Texture2D Tex, GraphicsDevice Device)> s_labelCache = new();
    private const int MaxCacheEntries = 128;

    public static readonly Color HeaderBg = new(55, 58, 65);
    public static readonly Color SectionBg = new(48, 51, 56);
    public static readonly Color RowBg = new(42, 45, 50);
    public static readonly Color Separator = new(35, 38, 42);
    public static readonly Color TextColor = new(220, 220, 220);
    public static readonly Color FoldoutArrow = new(180, 185, 195);
    public static readonly Color ControlBg = new(58, 62, 70);
    public static readonly Color ControlBorder = new(70, 74, 82);
    public static readonly Color DropdownHoverBg = new(65, 70, 78);

    public static Texture2D? GetLabelTexture(GraphicsDevice device, string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        if (s_labelCache.TryGetValue(text, out var entry) && entry.Device == device && !entry.Tex.IsDisposed)
            return entry.Tex;

        if (s_labelCache.Count >= MaxCacheEntries)
        {
            var first = s_labelCache.First();
            first.Value.Tex.Dispose();
            s_labelCache.Remove(first.Key);
        }

        var tex = CreateLabelTexture(device, text);
        if (tex != null)
            s_labelCache[text] = (tex, device);
        return tex;
    }

    private static Texture2D? CreateLabelTexture(GraphicsDevice device, string text)
    {
        try
        {
            using var font = new Font("Segoe UI", FontSize, FontStyle.Regular);
            using var bmp = new Bitmap(1, 1);
            using (var g = Graphics.FromImage(bmp))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                var size = g.MeasureString(text, font);
                int w = (int)Math.Ceiling(size.Width) + 2;
                int h = (int)Math.Ceiling(size.Height) + 2;
                if (w <= 0 || h <= 0) return null;
                using var bitmap = new Bitmap(w, h);
                using (var g2 = Graphics.FromImage(bitmap))
                {
                    g2.Clear(System.Drawing.Color.Transparent);
                    g2.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    using var whiteBrush = new SolidBrush(System.Drawing.Color.White);
                    g2.DrawString(text, font, whiteBrush, 0, 0);
                }
                return BitmapToTexture(device, bitmap, w, h);
            }
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

    public static void DrawLabel(SpriteBatch sb, GraphicsDevice device, int x, int y, string text, Texture2D? pixel)
    {
        DrawLabel(sb, device, x, y, text, pixel, TextColor);
    }

    /// <summary>Draw a label with an explicit tint color (e.g. for overlays/dialogs where TextColor may not be visible).</summary>
    public static void DrawLabel(SpriteBatch sb, GraphicsDevice device, int x, int y, string text, Texture2D? pixel, Color tint)
    {
        var tex = GetLabelTexture(device, text);
        if (tex != null)
            sb.Draw(tex, new Vector2(x, y), tint);
    }

    /// <summary>Draw a section header (e.g. track type name). Returns the height used.</summary>
    public static int DrawHeader(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, int x, int y, int w, string title, ref int cursorY)
    {
        int h = 22;
        sb.Draw(pixel, new Rectangle(x, y, w, h), HeaderBg);
        DrawLabel(sb, device, x + 6, y + 2, title, pixel);
        cursorY = y + h;
        return h;
    }

    /// <summary>Draw a 1px horizontal separator. Returns height (1).</summary>
    public static int DrawSeparator(SpriteBatch sb, Texture2D pixel, int x, int y, int w, ref int cursorY)
    {
        sb.Draw(pixel, new Rectangle(x, y, w, 1), Separator);
        cursorY = y + 1;
        return 1;
    }

    /// <summary>Draw a foldout row. Returns the row bounds for hit-testing (caller toggles expanded on click).</summary>
    public static Rectangle DrawFoldout(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, int x, int y, int w, string label, bool expanded, ref int cursorY)
    {
        int rowH = RowHeight;
        var rowRect = new Rectangle(x, y, w, rowH);
        sb.Draw(pixel, rowRect, SectionBg);

        // Arrow: right when collapsed, down when expanded
        int ax = x + 4;
        int ay = y + rowH / 2;
        if (expanded)
        {
            for (int i = 0; i <= 4; i++)
            {
                sb.Draw(pixel, new Rectangle(ax + i, ay - 4 + i, 1, 1), FoldoutArrow);
                sb.Draw(pixel, new Rectangle(ax + i, ay + 4 - i, 1, 1), FoldoutArrow);
            }
        }
        else
        {
            for (int i = 0; i <= 4; i++)
            {
                sb.Draw(pixel, new Rectangle(ax - 4 + i, ay - i, 1, 1), FoldoutArrow);
                sb.Draw(pixel, new Rectangle(ax - 4 + i, ay + i, 1, 1), FoldoutArrow);
            }
        }

        DrawLabel(sb, device, x + Indent, y + 2, label, pixel);
        cursorY = y + rowH;
        return rowRect;
    }

    /// <summary>Draw a simple label row (e.g. "Speed"). Returns height.</summary>
    public static int DrawRowLabel(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, int x, int y, int w, string label, ref int cursorY)
    {
        sb.Draw(pixel, new Rectangle(x, y, w, RowHeight), RowBg);
        DrawLabel(sb, device, x + Padding, y + 2, label, pixel);
        cursorY = y + RowHeight;
        return RowHeight;
    }

    /// <summary>Draw an enum dropdown row: label on left, value (clickable) on right with dropdown arrow. Returns the bounds of the value button for hit-testing.</summary>
    public static Rectangle DrawEnumRow(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, int x, int y, int w, string label, string valueText, ref int cursorY)
    {
        sb.Draw(pixel, new Rectangle(x, y, w, RowHeight), RowBg);
        DrawLabel(sb, device, x + Padding, y + 2, label, pixel);
        int valueW = Math.Max(80, w - LabelWidth - Padding * 2);
        var valueRect = new Rectangle(x + w - valueW - Padding, y + 2, valueW, RowHeight - 4);
        sb.Draw(pixel, valueRect, ControlBg);
        sb.Draw(pixel, new Rectangle(valueRect.X - 1, valueRect.Y - 1, valueRect.Width + 2, valueRect.Height + 2), ControlBorder);
        DrawLabel(sb, device, valueRect.X + 4, valueRect.Y + 1, valueText, pixel);
        // Dropdown arrow (small triangle)
        int ax = valueRect.Right - 12;
        int ay = valueRect.Y + valueRect.Height / 2;
        for (int i = -3; i <= 3; i++)
            for (int j = 0; j <= 4 - Math.Abs(i); j++)
                sb.Draw(pixel, new Rectangle(ax + i, ay - 2 + j, 1, 1), FoldoutArrow);
        cursorY = y + RowHeight;
        return valueRect;
    }

    /// <summary>Draw an open dropdown list of options below the current cursor. Returns the full dropdown rect and per-option rects for hit-testing. Optional mouse position highlights the option under the cursor.</summary>
    public static (Rectangle dropdownRect, Rectangle[] optionRects) DrawDropdownList(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, int x, int y, int w, string[] options, int selectedIndex, ref int cursorY, Microsoft.Xna.Framework.Point? mouse = null)
    {
        int listH = options.Length * RowHeight;
        var dropdownRect = new Rectangle(x, y, w, listH);
        sb.Draw(pixel, dropdownRect, SectionBg);
        sb.Draw(pixel, new Rectangle(x - 1, y - 1, w + 2, listH + 2), ControlBorder);
        var optionRects = new Rectangle[options.Length];
        for (int i = 0; i < options.Length; i++)
        {
            var rowRect = new Rectangle(x, y + i * RowHeight, w, RowHeight);
            optionRects[i] = rowRect;
            bool hover = mouse.HasValue && rowRect.Contains(mouse.Value);
            if (hover)
                sb.Draw(pixel, rowRect, DropdownHoverBg);
            else if (i == selectedIndex)
                sb.Draw(pixel, rowRect, ControlBg);
            DrawLabel(sb, device, x + Padding, y + i * RowHeight + 2, options[i], pixel);
        }
        cursorY = y + listH;
        return (dropdownRect, optionRects);
    }

    /// <summary>Draw a float row: label + value text. Returns the value field bounds for hit-test / edit.</summary>
    public static Rectangle DrawFloatRow(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, int x, int y, int w, string label, string valueText, ref int cursorY)
    {
        sb.Draw(pixel, new Rectangle(x, y, w, RowHeight), RowBg);
        DrawLabel(sb, device, x + Padding, y + 2, label, pixel);
        int valueW = Math.Max(60, w - LabelWidth - Padding * 2);
        var valueRect = new Rectangle(x + w - valueW - Padding, y + 2, valueW, RowHeight - 4);
        sb.Draw(pixel, valueRect, ControlBg);
        sb.Draw(pixel, new Rectangle(valueRect.X - 1, valueRect.Y - 1, valueRect.Width + 2, valueRect.Height + 2), ControlBorder);
        DrawLabel(sb, device, valueRect.X + 4, valueRect.Y + 1, valueText, pixel);
        cursorY = y + RowHeight;
        return valueRect;
    }

    /// <summary>Draw a string row: label + value text (e.g. for FMOD path). Returns the value field bounds for hit-test / edit.</summary>
    public static Rectangle DrawStringRow(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, int x, int y, int w, string label, string valueText, ref int cursorY)
    {
        sb.Draw(pixel, new Rectangle(x, y, w, RowHeight), RowBg);
        DrawLabel(sb, device, x + Padding, y + 2, label, pixel);
        int valueW = Math.Max(80, w - LabelWidth - Padding * 2);
        var valueRect = new Rectangle(x + w - valueW - Padding, y + 2, valueW, RowHeight - 4);
        sb.Draw(pixel, valueRect, ControlBg);
        sb.Draw(pixel, new Rectangle(valueRect.X - 1, valueRect.Y - 1, valueRect.Width + 2, valueRect.Height + 2), ControlBorder);
        string displayText = string.IsNullOrEmpty(valueText) ? "..." : (valueText.Length > 24 ? valueText[..24] + "…" : valueText);
        DrawLabel(sb, device, valueRect.X + 4, valueRect.Y + 1, displayText, pixel);
        cursorY = y + RowHeight;
        return valueRect;
    }

    /// <summary>Draw three float fields for Vector3 (X, Y, Z). Each on its own row with small label. Advances cursorY by 3*RowHeight. Optional override strings for editing.</summary>
    public static void DrawVector3Rows(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, int x, int y, int w, Vector3 v, ref int cursorY, string? overrideX = null, string? overrideY = null, string? overrideZ = null)
    {
        DrawVector3Row(sb, pixel, device, x, y, w, "X", v.X, ref cursorY, overrideX);
        DrawVector3Row(sb, pixel, device, x, cursorY, w, "Y", v.Y, ref cursorY, overrideY);
        DrawVector3Row(sb, pixel, device, x, cursorY, w, "Z", v.Z, ref cursorY, overrideZ);
    }

    private static void DrawVector3Row(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, int x, int y, int w, string axis, float value, ref int cursorY, string? valueOverride = null)
    {
        string valueText = valueOverride ?? value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        sb.Draw(pixel, new Rectangle(x, y, w, RowHeight), RowBg);
        DrawLabel(sb, device, x + Padding, y + 2, axis, pixel);
        int valueW = Math.Max(60, w / 2);
        var valueRect = new Rectangle(x + w - valueW - Padding, y + 2, valueW, RowHeight - 4);
        sb.Draw(pixel, valueRect, ControlBg);
        sb.Draw(pixel, new Rectangle(valueRect.X - 1, valueRect.Y - 1, valueRect.Width + 2, valueRect.Height + 2), ControlBorder);
        DrawLabel(sb, device, valueRect.X + 4, valueRect.Y + 1, valueText, pixel);
        cursorY = y + RowHeight;
    }
}
