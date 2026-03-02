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
    public const int RowHeight = 26;
    public const int Indent = 14;
    public const int LabelWidth = 100;
    public const int Padding = 10;
    public const int FontSize = 12;

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

    /// <summary>Clears the label texture cache. Call when entering play or after device reset to avoid stale/wrong-sized textures.</summary>
    public static void InvalidateLabelCache()
    {
        foreach (var kv in s_labelCache)
        {
            try { kv.Value.Tex.Dispose(); } catch { /* ignore */ }
        }
        s_labelCache.Clear();
    }

    public static Texture2D? GetLabelTexture(GraphicsDevice device, string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        // Evict stale entries (wrong device or disposed) so we don't hold dead textures
        if (s_labelCache.TryGetValue(text, out var entry))
        {
            if (entry.Device == device && !entry.Tex.IsDisposed)
                return entry.Tex;
            s_labelCache.Remove(text);
            try { entry.Tex.Dispose(); } catch { /* already disposed */ }
        }

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

    /// <summary>Normalize text for GDI+ rendering to avoid black squares from missing glyphs (e.g. Unicode ellipsis).</summary>
    private static string NormalizeLabelText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Replace("\u2026", "..."); // Unicode ellipsis -> ASCII
    }

    private const int MaxLabelTextureSize = 512;

    private static Texture2D? CreateLabelTexture(GraphicsDevice device, string text)
    {
        string renderText = NormalizeLabelText(text);
        if (string.IsNullOrEmpty(renderText)) return null;
        try
        {
            using var font = CreateLabelFont();
            if (font == null) return null;
            using var bitmap = new Bitmap(1, 1);
            bitmap.SetResolution(96, 96);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                g.PageUnit = GraphicsUnit.Pixel;
                g.PageScale = 1f;
                var size = g.MeasureString(renderText, font);
                int w = Math.Min(MaxLabelTextureSize, (int)Math.Ceiling(size.Width) + 2);
                int h = Math.Min(MaxLabelTextureSize, (int)Math.Ceiling(size.Height) + 2);
                if (w <= 0 || h <= 0) return null;
                using var drawBitmap = new Bitmap(w, h);
                drawBitmap.SetResolution(96, 96);
                using (var g2 = Graphics.FromImage(drawBitmap))
                {
                    g2.Clear(System.Drawing.Color.Transparent);
                    g2.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g2.PageUnit = GraphicsUnit.Pixel;
                    g2.PageScale = 1f;
                    g2.DrawString(renderText, font, System.Drawing.Brushes.White, 0, 0);
                }
                return BitmapToTexture(device, drawBitmap, w, h);
            }
        }
        catch
        {
            return null;
        }
    }

    private static Font? CreateLabelFont()
    {
        try
        {
            return new Font("Segoe UI", FontSize, FontStyle.Regular);
        }
        catch
        {
            try
            {
                return new Font("Arial", FontSize, FontStyle.Regular);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>Copy bitmap ARGB directly to Texture2D (same approach as TransportBarPanel BPM/timecode).</summary>
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

    /// <summary>Measure label text size using the same font as DrawLabel. Used for scrollable content width.</summary>
    public static (int Width, int Height) MeasureLabel(string text)
    {
        string renderText = NormalizeLabelText(text ?? "");
        if (string.IsNullOrEmpty(renderText)) return (0, 0);
        try
        {
            using var font = CreateLabelFont();
            if (font == null) return (0, 0);
            using var bitmap = new Bitmap(1, 1);
            bitmap.SetResolution(96, 96);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                g.PageUnit = GraphicsUnit.Pixel;
                g.PageScale = 1f;
                var size = g.MeasureString(renderText, font);
                return (Math.Min(MaxLabelTextureSize, (int)Math.Ceiling(size.Width) + 2), Math.Min(MaxLabelTextureSize, (int)Math.Ceiling(size.Height) + 2));
            }
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <summary>Draw a label with horizontal scroll: only the portion [scrollX, scrollX+visibleWidth) is drawn. Use for wide console lines.</summary>
    public static void DrawLabelScrollable(SpriteBatch sb, GraphicsDevice device, int x, int y, int visibleWidth, int scrollX, string text, Texture2D? pixel, Color tint)
    {
        var tex = GetLabelTexture(device, text);
        if (tex == null) return;
        int texW = tex.Width;
        int texH = tex.Height;
        if (scrollX >= texW || visibleWidth <= 0) return;
        int srcX = scrollX;
        int srcW = Math.Min(visibleWidth, texW - scrollX);
        var src = new Rectangle(srcX, 0, srcW, texH);
        var dest = new Rectangle(x, y, srcW, texH);
        sb.Draw(tex, dest, src, tint);
    }

    /// <summary>Draw a label clipped to maxWidth and maxHeight so it never overflows (e.g. dropdown text). Uses 1:1 source rect so no scaling.</summary>
    public static void DrawLabelClipped(SpriteBatch sb, GraphicsDevice device, int x, int y, int maxWidth, int maxHeight, string text, Texture2D? pixel, Color? tint = null)
    {
        var tex = GetLabelTexture(device, text);
        if (tex == null) return;
        int w = Math.Min(tex.Width, maxWidth);
        int h = Math.Min(tex.Height, maxHeight);
        if (w <= 0 || h <= 0) return;
        var dest = new Rectangle(x, y, w, h);
        var src = new Rectangle(0, 0, w, h);
        sb.Draw(tex, dest, src, tint ?? TextColor);
    }

    /// <summary>Draw a label scaled to fit within maxWidth and maxHeight so the full text is visible (no clipping).</summary>
    public static void DrawLabelScaledToFit(SpriteBatch sb, GraphicsDevice device, int x, int y, int maxWidth, int maxHeight, string text, Texture2D? pixel, Color? tint = null)
    {
        var tex = GetLabelTexture(device, text);
        if (tex == null) return;
        float scale = Math.Min(1f, Math.Min((float)maxWidth / tex.Width, (float)maxHeight / tex.Height));
        int w = (int)(tex.Width * scale);
        int h = (int)(tex.Height * scale);
        if (w <= 0 || h <= 0) return;
        var dest = new Rectangle(x, y, w, h);
        var src = new Rectangle(0, 0, tex.Width, tex.Height);
        sb.Draw(tex, dest, src, tint ?? TextColor);
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

    /// <summary>Draw a foldout row. Returns the row bounds for hit-testing (caller toggles expanded on click). When canExpand is false (empty section), no arrow is drawn.</summary>
    public static Rectangle DrawFoldout(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, int x, int y, int w, string label, bool expanded, ref int cursorY, bool canExpand = true)
    {
        int rowH = RowHeight;
        var rowRect = new Rectangle(x, y, w, rowH);
        sb.Draw(pixel, rowRect, SectionBg);

        if (canExpand)
        {
            // Arrow: right when collapsed, down when expanded
            int ax = x + 4;
            int ay = y + rowH / 2;
            if (expanded)
            {
                // Down-pointing triangle: tip at (ax, ay+4), base at top
                for (int i = 0; i <= 4; i++)
                {
                    int rowY = ay - 4 + i;
                    int halfW = 4 - i;
                    for (int j = -halfW; j <= halfW; j++)
                        sb.Draw(pixel, new Rectangle(ax + j, rowY, 1, 1), FoldoutArrow);
                }
            }
            else
            {
                // Right-pointing triangle: tip at (ax+4, ay), base at left
                for (int i = 0; i <= 4; i++)
                {
                    int colX = ax + i;
                    int halfH = 4 - i;
                    for (int j = -halfH; j <= halfH; j++)
                        sb.Draw(pixel, new Rectangle(colX, ay + j, 1, 1), FoldoutArrow);
                }
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

    /// <summary>Draw an enum dropdown row: label on left, value (clickable) on right with optional dropdown arrow. Returns the bounds of the value button for hit-testing. When showDropdownArrow is false (e.g. no options), the arrow is omitted.</summary>
    public static Rectangle DrawEnumRow(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, int x, int y, int w, string label, string valueText, ref int cursorY, bool showDropdownArrow = true)
    {
        sb.Draw(pixel, new Rectangle(x, y, w, RowHeight), RowBg);
        DrawLabel(sb, device, x + Padding, y + 2, label, pixel);
        int valueW = Math.Max(80, w - LabelWidth - Padding * 2);
        var valueRect = new Rectangle(x + w - valueW - Padding, y + 2, valueW, RowHeight - 4);
        sb.Draw(pixel, valueRect, ControlBg);
        sb.Draw(pixel, new Rectangle(valueRect.X - 1, valueRect.Y - 1, valueRect.Width + 2, valueRect.Height + 2), ControlBorder);
        DrawLabel(sb, device, valueRect.X + 4, valueRect.Y + 1, valueText, pixel);
        if (showDropdownArrow)
        {
            int ax = valueRect.Right - 12;
            int ay = valueRect.Y + valueRect.Height / 2;
            for (int i = -3; i <= 3; i++)
                for (int j = 0; j <= 4 - Math.Abs(i); j++)
                    sb.Draw(pixel, new Rectangle(ax + i, ay - 2 + j, 1, 1), FoldoutArrow);
        }
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
    public static Rectangle DrawFloatRow(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, int x, int y, int w, string label, string valueText, ref int cursorY, bool showCaret = false)
    {
        sb.Draw(pixel, new Rectangle(x, y, w, RowHeight), RowBg);
        DrawLabel(sb, device, x + Padding, y + 2, label, pixel);
        int valueW = Math.Max(60, w - LabelWidth - Padding * 2);
        var valueRect = new Rectangle(x + w - valueW - Padding, y + 2, valueW, RowHeight - 4);
        sb.Draw(pixel, valueRect, ControlBg);
        sb.Draw(pixel, new Rectangle(valueRect.X - 1, valueRect.Y - 1, valueRect.Width + 2, valueRect.Height + 2), ControlBorder);
        DrawLabel(sb, device, valueRect.X + 4, valueRect.Y + 1, valueText, pixel);
        if (showCaret)
            DrawCaret(sb, pixel, device, valueRect, valueText, TextColor);
        cursorY = y + RowHeight;
        return valueRect;
    }

    /// <summary>Button width for random toggle to the right of a value field.</summary>
    public const int RandomToggleButtonWidth = 24;

    /// <summary>Draw a float row with a randomize toggle button to the right. Value area is narrowed to fit the button. Returns (valueRect, randomButtonRect).</summary>
    public static (Rectangle valueRect, Rectangle randomButtonRect) DrawFloatRowWithRandomToggle(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, int x, int y, int w, string label, string valueText, bool randomOn, ref int cursorY, bool showCaret = false)
    {
        sb.Draw(pixel, new Rectangle(x, y, w, RowHeight), RowBg);
        DrawLabel(sb, device, x + Padding, y + 2, label, pixel);
        int valueW = Math.Max(60, w - LabelWidth - Padding * 2 - RandomToggleButtonWidth - 4);
        int buttonX = x + w - Padding - RandomToggleButtonWidth;
        var valueRect = new Rectangle(buttonX - valueW - 4, y + 2, valueW, RowHeight - 4);
        var randomButtonRect = new Rectangle(buttonX, y + 2, RandomToggleButtonWidth, RowHeight - 4);
        sb.Draw(pixel, valueRect, ControlBg);
        sb.Draw(pixel, new Rectangle(valueRect.X - 1, valueRect.Y - 1, valueRect.Width + 2, valueRect.Height + 2), ControlBorder);
        DrawLabel(sb, device, valueRect.X + 4, valueRect.Y + 1, valueText, pixel);
        if (showCaret)
            DrawCaret(sb, pixel, device, valueRect, valueText, TextColor);
        // Random button: "R" — darker when enabled (random on)
        sb.Draw(pixel, randomButtonRect, randomOn ? SectionBg : ControlBg);
        sb.Draw(pixel, new Rectangle(randomButtonRect.X - 1, randomButtonRect.Y - 1, randomButtonRect.Width + 2, randomButtonRect.Height + 2), ControlBorder);
        DrawLabel(sb, device, randomButtonRect.X + 4, randomButtonRect.Y + 1, "R", pixel);
        cursorY = y + RowHeight;
        return (valueRect, randomButtonRect);
    }

    /// <summary>Draw a string row: label + value text (e.g. for FMOD path). Returns the value field bounds for hit-test / edit.</summary>
    public static Rectangle DrawStringRow(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, int x, int y, int w, string label, string valueText, ref int cursorY, bool showCaret = false)
    {
        sb.Draw(pixel, new Rectangle(x, y, w, RowHeight), RowBg);
        DrawLabel(sb, device, x + Padding, y + 2, label, pixel);
        int valueW = Math.Max(80, w - LabelWidth - Padding * 2);
        var valueRect = new Rectangle(x + w - valueW - Padding, y + 2, valueW, RowHeight - 4);
        sb.Draw(pixel, valueRect, ControlBg);
        sb.Draw(pixel, new Rectangle(valueRect.X - 1, valueRect.Y - 1, valueRect.Width + 2, valueRect.Height + 2), ControlBorder);
        string displayText = showCaret && string.IsNullOrEmpty(valueText)
            ? ""
            : (string.IsNullOrEmpty(valueText) ? "..." : (valueText.Length > 24 ? valueText[..24] + "…" : valueText));
        DrawLabel(sb, device, valueRect.X + 4, valueRect.Y + 1, displayText, pixel);
        if (showCaret)
            DrawCaret(sb, pixel, device, valueRect, displayText, TextColor);
        cursorY = y + RowHeight;
        return valueRect;
    }

    private static void DrawCaret(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, Rectangle valueRect, string text, Color color)
    {
        int caretX = valueRect.X + 4;
        if (!string.IsNullOrEmpty(text))
        {
            var tex = GetLabelTexture(device, text);
            if (tex != null)
                caretX += tex.Width;
        }
        int h = Math.Max(2, valueRect.Height - 2);
        sb.Draw(pixel, new Rectangle(caretX, valueRect.Y + 2, 1, h), color);
    }

    /// <summary>Draw three float fields for Vector3 (X, Y, Z). Each on its own row with small label. Advances cursorY by 3*RowHeight. Optional override strings and caret flags for editing.</summary>
    public static void DrawVector3Rows(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, int x, int y, int w, Vector3 v, ref int cursorY, string? overrideX = null, string? overrideY = null, string? overrideZ = null, bool showCaretX = false, bool showCaretY = false, bool showCaretZ = false)
    {
        DrawVector3Row(sb, pixel, device, x, y, w, "X", v.X, ref cursorY, overrideX, showCaretX);
        DrawVector3Row(sb, pixel, device, x, cursorY, w, "Y", v.Y, ref cursorY, overrideY, showCaretY);
        DrawVector3Row(sb, pixel, device, x, cursorY, w, "Z", v.Z, ref cursorY, overrideZ, showCaretZ);
    }

    private static void DrawVector3Row(SpriteBatch sb, Texture2D pixel, GraphicsDevice device, int x, int y, int w, string axis, float value, ref int cursorY, string? valueOverride = null, bool showCaret = false)
    {
        string valueText = valueOverride ?? value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        sb.Draw(pixel, new Rectangle(x, y, w, RowHeight), RowBg);
        DrawLabel(sb, device, x + Padding, y + 2, axis, pixel);
        int valueW = Math.Max(60, w / 2);
        var valueRect = new Rectangle(x + w - valueW - Padding, y + 2, valueW, RowHeight - 4);
        sb.Draw(pixel, valueRect, ControlBg);
        sb.Draw(pixel, new Rectangle(valueRect.X - 1, valueRect.Y - 1, valueRect.Width + 2, valueRect.Height + 2), ControlBorder);
        DrawLabel(sb, device, valueRect.X + 4, valueRect.Y + 1, valueText, pixel);
        if (showCaret)
            DrawCaret(sb, pixel, device, valueRect, valueText, TextColor);
        cursorY = y + RowHeight;
    }
}
