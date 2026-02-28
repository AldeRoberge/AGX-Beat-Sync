using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.UI;

public class TransportBarPanel : PanelBase
{
    private const int ButtonWidth = 28;
    private const int PlayPauseButtonWidth = 28;
    private const int BpmAreaWidth = 120;
    private const int TimeAreaWidth = 140;
    private const int Padding = 8;
    private const int VolumeIconSize = 16;
    private const int VolumeSliderWidth = 88;
    private const int SliderTrackHeight = 16;

    private float _volume = 1f;
    private bool _volumeSliderDragging;
    /// <summary>Game time until which to show "Volume: N%" in the status bar (set while dragging).</summary>
    private double _volumeStatusVisibleUntil;

    /// <summary>Master volume (0.0 to 1.0). Synced to audio playback by the game.</summary>
    public float Volume { get => _volume; set => _volume = Math.Clamp(value, 0f, 1f); }

    /// <summary>When the user is changing volume, returns e.g. "Volume: 75%" for the status bar; otherwise null.</summary>
    public string? GetVolumeStatusText(double totalSeconds)
    {
        if (totalSeconds > _volumeStatusVisibleUntil) return null;
        return "Volume: " + (int)Math.Round(_volume * 100) + "%";
    }

    private Texture2D? _bpmTextTexture;
    private string _bpmCachedString = "";
    private int _bpmCachedWidth = -1;
    private int _bpmCachedHeight = -1;
    private Texture2D? _timeTextTexture;
    private string _timeCachedString = "";
    private int _timeCachedWidth = -1;
    private int _timeCachedHeight = -1;
    private Texture2D? _circleTexture;
    private Texture2D? _playIconTexture;
    private const int PlayIconSize = 14;

    public Project? Project { get; set; }
    public Transport? Transport { get; set; }
    public Input.InputManager? Input { get; set; }
    /// <summary>When true, show REC indicator; keys 1-9 and 0 add events to tracks while playing.</summary>
    public bool RecordMode { get; set; }

    public TransportBarPanel()
    {
        Title = "Transport";
        HeaderHeight = 32;
        BackgroundColor = new Color(45, 48, 52);
        HeaderColor = new Color(45, 48, 52);
    }

    private Rectangle GetVolumeIconRect()
    {
        int x = Bounds.X + Padding;
        int y = Bounds.Y + (Bounds.Height - VolumeIconSize) / 2;
        return new Rectangle(x, y, VolumeIconSize, VolumeIconSize);
    }

    private Rectangle GetVolumeSliderTrack()
    {
        var icon = GetVolumeIconRect();
        int x = icon.Right + 4;
        int y = Bounds.Y + (Bounds.Height - SliderTrackHeight) / 2;
        int width = VolumeSliderWidth - (icon.Width + 4 + Padding);
        return new Rectangle(x, y, Math.Max(0, width), SliderTrackHeight);
    }

    private Rectangle GetBpmArea()
    {
        int x = Bounds.X + Padding + VolumeSliderWidth;
        int y = Bounds.Y + (Bounds.Height - 24) / 2;
        return new Rectangle(x, y, BpmAreaWidth, 24);
    }

    private Rectangle GetBpmMinusButton()
    {
        var area = GetBpmArea();
        return new Rectangle(area.X, area.Y, ButtonWidth, area.Height);
    }

    private Rectangle GetBpmPlusButton()
    {
        var area = GetBpmArea();
        return new Rectangle(area.X + area.Width - ButtonWidth, area.Y, ButtonWidth, area.Height);
    }

    private Rectangle GetBpmValueRect()
    {
        var area = GetBpmArea();
        return new Rectangle(area.X + ButtonWidth + 2, area.Y, area.Width - 2 * ButtonWidth - 4, area.Height);
    }

    /// <summary>Invoked when user clicks the BPM value area to type a new value.</summary>
    public Action? BpmEditRequested { get; set; }
    /// <summary>Invoked when user clicks the time area to type a position (HH:mm:ss:frame).</summary>
    public Action? TimeEditRequested { get; set; }
    /// <summary>Invoked when user clicks the play/pause button (toggle).</summary>
    public Action? OnPlayPauseToggle { get; set; }
    /// <summary>Invoked when user clicks the REC button (toggle record mode).</summary>
    public Action? OnRecordToggle { get; set; }

    private Rectangle GetPlayPauseButtonRect()
    {
        var bpm = GetBpmArea();
        int x = bpm.Right + Padding;
        int y = Bounds.Y + (Bounds.Height - 24) / 2;
        return new Rectangle(x, y, PlayPauseButtonWidth, 24);
    }

    private const int RecButtonSize = 24;

    private Rectangle GetRecButtonRect()
    {
        var playPause = GetPlayPauseButtonRect();
        int x = playPause.Right + Padding;
        int y = Bounds.Y + (Bounds.Height - RecButtonSize) / 2;
        return new Rectangle(x, y, RecButtonSize, RecButtonSize);
    }

    private Rectangle GetTimeAreaRect()
    {
        var rec = GetRecButtonRect();
        int x = rec.Right + Padding;
        int y = Bounds.Y + (Bounds.Height - 24) / 2;
        return new Rectangle(x, y, TimeAreaWidth, 24);
    }

    public override string? GetHoverText(Point mouse)
    {
        if (!ContainsPoint(mouse)) return null;
        var volIcon = GetVolumeIconRect();
        var volTrack = GetVolumeSliderTrack();
        if (volIcon.Contains(mouse) || volTrack.Contains(mouse)) return "Volume";
        var minus = GetBpmMinusButton();
        if (minus.Contains(mouse)) return "BPM −";
        var plus = GetBpmPlusButton();
        if (plus.Contains(mouse)) return "BPM +";
        if (GetBpmValueRect().Contains(mouse)) return "BPM — click to edit";
        if (GetPlayPauseButtonRect().Contains(mouse)) return Transport?.IsPlaying == true ? "Pause (Space)" : "Play (Space)";
        if (GetRecButtonRect().Contains(mouse)) return RecordMode ? "Record mode on (R). Press 1–9 or 0 while playing to add event to track." : "Record mode off (R). Click or press R to enable.";
        if (GetTimeAreaRect().Contains(mouse)) return "Time — click to go to position";
        return "Transport";
    }

    public override void Update(GameTime gameTime)
    {
        if (Input == null) return;

        var volTrack = GetVolumeSliderTrack();
        if (Input.MouseLeftPressed && volTrack.Contains(Input.MousePosition))
        {
            _volumeSliderDragging = true;
            _volumeStatusVisibleUntil = gameTime.TotalGameTime.TotalSeconds + 1.5;
        }
        if (Input.MouseLeftReleased)
            _volumeSliderDragging = false;

        if (_volumeSliderDragging && volTrack.Width > 0)
        {
            int mx = Math.Clamp(Input.MousePosition.X, volTrack.X, volTrack.Right);
            Volume = (float)(mx - volTrack.X) / volTrack.Width;
            _volumeStatusVisibleUntil = gameTime.TotalGameTime.TotalSeconds + 1.5;
        }

        if (!Input.MouseLeftPressed) return;
        if (!ContainsPoint(Input.MousePosition)) return;

        if (GetPlayPauseButtonRect().Contains(Input.MousePosition))
        {
            OnPlayPauseToggle?.Invoke();
            return;
        }
        if (GetRecButtonRect().Contains(Input.MousePosition))
        {
            OnRecordToggle?.Invoke();
            return;
        }

        if (Transport == null || Project == null) return;

        var minus = GetBpmMinusButton();
        var plus = GetBpmPlusButton();
        double step = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift) ? 5.0 : 1.0;

        if (minus.Contains(Input.MousePosition))
        {
            double bpm = Math.Max(20, Transport.BPM - step);
            Transport.BPM = bpm;
            Project.BPM = (float)bpm;
        }
        else if (plus.Contains(Input.MousePosition))
        {
            double bpm = Math.Min(999, Transport.BPM + step);
            Transport.BPM = bpm;
            Project.BPM = (float)bpm;
        }
        else if (GetBpmValueRect().Contains(Input.MousePosition))
        {
            BpmEditRequested?.Invoke();
        }
        else if (GetTimeAreaRect().Contains(Input.MousePosition))
        {
            TimeEditRequested?.Invoke();
        }
    }

    protected override void DrawPanelBackground(SpriteBatch spriteBatch)
    {
        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);
        spriteBatch.Draw(pixel, Bounds, HeaderColor);
        DrawBorder(spriteBatch, pixel);
    }

    protected override void DrawContent(SpriteBatch spriteBatch)
    {
        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);

        // Volume icon + slider (track + fill + thumb)
        var volIconRect = GetVolumeIconRect();
        DrawVolumeIcon(spriteBatch, pixel, volIconRect, new Color(200, 202, 208));
        var volTrack = GetVolumeSliderTrack();
        spriteBatch.Draw(pixel, volTrack, new Color(55, 58, 64));
        var fillWidth = (int)(volTrack.Width * _volume);
        if (fillWidth > 0)
            spriteBatch.Draw(pixel, new Rectangle(volTrack.X, volTrack.Y, fillWidth, volTrack.Height), new Color(90, 140, 200));
        int thumbX = volTrack.X + (int)(_volume * volTrack.Width) - 2;
        if (thumbX >= volTrack.X && thumbX + 4 <= volTrack.Right)
            spriteBatch.Draw(pixel, new Rectangle(thumbX, volTrack.Y - 2, 4, volTrack.Height + 4), new Color(200, 202, 208));

        var minus = GetBpmMinusButton();
        var plus = GetBpmPlusButton();
        var area = GetBpmArea();

        // BPM area background
        spriteBatch.Draw(pixel, area, new Color(38, 41, 46));
        // Minus button
        spriteBatch.Draw(pixel, minus, new Color(48, 52, 58));
        spriteBatch.Draw(pixel, new Rectangle(minus.Center.X - 5, minus.Center.Y - 1, 10, 2), new Color(200, 202, 208));
        // Plus button
        spriteBatch.Draw(pixel, plus, new Color(48, 52, 58));
        spriteBatch.Draw(pixel, new Rectangle(plus.Center.X - 5, plus.Center.Y - 1, 10, 2), new Color(200, 202, 208));
        spriteBatch.Draw(pixel, new Rectangle(plus.Center.X - 1, plus.Center.Y - 5, 2, 10), new Color(200, 202, 208));
        // Center label area: draw BPM value as text
        var center = GetBpmValueRect();
        spriteBatch.Draw(pixel, center, new Color(32, 35, 40));

        string bpmString = Transport != null ? FormatBpm(Transport.BPM) : "120";
        EnsureBpmTextTexture(spriteBatch.GraphicsDevice, bpmString, center);
        if (_bpmTextTexture != null)
        {
            int x = center.X + (center.Width - _bpmTextTexture.Width) / 2;
            int y = center.Y + (center.Height - _bpmTextTexture.Height) / 2;
            spriteBatch.Draw(_bpmTextTexture, new Rectangle(x, y, _bpmTextTexture.Width, _bpmTextTexture.Height), Color.White);
        }

        // Play/Pause button (left of time)
        var playPauseRect = GetPlayPauseButtonRect();
        bool playPauseHover = playPauseRect.Contains(Input?.MousePosition ?? Point.Zero);
        spriteBatch.Draw(pixel, playPauseRect, playPauseHover ? new Color(55, 58, 65) : new Color(48, 52, 58));
        var iconColor = new Color(200, 202, 208);
        if (Transport?.IsPlaying == true)
            DrawPauseIcon(spriteBatch, pixel, playPauseRect, iconColor);
        else
            DrawPlayIcon(spriteBatch, spriteBatch.GraphicsDevice, pixel, playPauseRect, iconColor);

        // REC circle button: gray when off, red when record mode on
        var recRect = GetRecButtonRect();
        bool recHover = recRect.Contains(Input?.MousePosition ?? Point.Zero);
        EnsureCircleTexture(spriteBatch.GraphicsDevice);
        if (_circleTexture != null)
        {
            Color recColor = RecordMode ? new Color(200, 50, 50) : (recHover ? new Color(90, 94, 100) : new Color(60, 64, 70));
            spriteBatch.Draw(_circleTexture, recRect, recColor);
        }

        // Time area (HH:mm:ss:frame — click to type and go to position)
        var timeArea = GetTimeAreaRect();
        spriteBatch.Draw(pixel, timeArea, new Color(55, 58, 64));
        string timeString = Transport != null ? TimeFormatHelper.Format(Transport.CurrentTime) : "00:00:00:00";
        EnsureTimeTextTexture(spriteBatch.GraphicsDevice, timeString, timeArea);
        if (_timeTextTexture != null)
        {
            int x = timeArea.X + (timeArea.Width - _timeTextTexture.Width) / 2;
            int y = timeArea.Y + (timeArea.Height - _timeTextTexture.Height) / 2;
            spriteBatch.Draw(_timeTextTexture, new Rectangle(x, y, _timeTextTexture.Width, _timeTextTexture.Height), Color.White);
        }

    }

    private static string FormatBpm(double bpm)
    {
        return bpm == Math.Floor(bpm) ? ((int)bpm).ToString() : bpm.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void EnsureBpmTextTexture(GraphicsDevice device, string text, Rectangle destRect)
    {
        int w = Math.Max(1, destRect.Width);
        int h = Math.Max(1, destRect.Height);
        if (_bpmCachedString == text && _bpmCachedWidth == w && _bpmCachedHeight == h && _bpmTextTexture != null && !_bpmTextTexture.IsDisposed)
            return;
        _bpmCachedString = text;
        _bpmCachedWidth = w;
        _bpmCachedHeight = h;
        _bpmTextTexture?.Dispose();
        _bpmTextTexture = CreateLabelTextTexture(device, text, w, h);
    }

    private void EnsureTimeTextTexture(GraphicsDevice device, string text, Rectangle destRect)
    {
        int w = Math.Max(1, destRect.Width);
        int h = Math.Max(1, destRect.Height);
        if (_timeCachedString == text && _timeCachedWidth == w && _timeCachedHeight == h && _timeTextTexture != null && !_timeTextTexture.IsDisposed)
            return;
        _timeCachedString = text;
        _timeCachedWidth = w;
        _timeCachedHeight = h;
        _timeTextTexture?.Dispose();
        _timeTextTexture = CreateLabelTextTexture(device, text, w, h);
    }

    private void EnsureCircleTexture(GraphicsDevice device)
    {
        if (_circleTexture != null && !_circleTexture.IsDisposed) return;
        _circleTexture?.Dispose();
        _circleTexture = CreateCircleTexture(device, RecButtonSize);
    }

    private void EnsurePlayIconTexture(GraphicsDevice device)
    {
        if (_playIconTexture != null && !_playIconTexture.IsDisposed) return;
        _playIconTexture?.Dispose();
        _playIconTexture = CreatePlayIconTexture(device);
    }

    private static Texture2D? CreatePlayIconTexture(GraphicsDevice device)
    {
        const int w = PlayIconSize;
        const int h = PlayIconSize;
        int maxW = Math.Max(2, (w * 55) / 100);
        int centerRow = (h - 1) / 2;
        int topHeight = Math.Max(1, centerRow);
        int bottomHeight = Math.Max(1, h - 1 - centerRow);
        var data = new Microsoft.Xna.Framework.Color[w * h];
        for (int i = 0; i < data.Length; i++)
            data[i] = Microsoft.Xna.Framework.Color.Transparent;
        // Build left-pointing triangle in texture (base at x=0, tip at x=maxW). We draw with FlipHorizontally so it displays right-pointing.
        for (int row = 0; row < h; row++)
        {
            int width;
            if (row <= centerRow)
                width = (maxW * row) / topHeight;
            else
                width = (maxW * (h - 1 - row)) / bottomHeight;
            if (width <= 0) continue;
            for (int x = 0; x < width; x++)
                data[row * w + x] = Microsoft.Xna.Framework.Color.White;
        }
        var tex = new Texture2D(device, w, h);
        tex.SetData(data);
        return tex;
    }

    private static Texture2D? CreateCircleTexture(GraphicsDevice device, int size)
    {
        var data = new Microsoft.Xna.Framework.Color[size * size];
        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float r = cx - 0.5f;
        float rSq = r * r;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                bool inside = (dx * dx + dy * dy) <= rSq;
                data[y * size + x] = inside ? Microsoft.Xna.Framework.Color.White : Microsoft.Xna.Framework.Color.Transparent;
            }
        }
        var tex = new Texture2D(device, size, size);
        tex.SetData(data);
        return tex;
    }

    private static Texture2D? CreateLabelTextTexture(GraphicsDevice device, string text, int width, int height)
    {
        try
        {
            int fontSize = Math.Max(8, Math.Min(14, height * 5 / 8));
            using var font = new Font("Segoe UI", fontSize, FontStyle.Regular);
            using var bitmap = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.DrawString(text, font, System.Drawing.Brushes.White, 0, 0);
            }

            var data = new Microsoft.Xna.Framework.Color[width * height];
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
                        data[i] = new Microsoft.Xna.Framework.Color(rawBytes[off + 2], rawBytes[off + 1], rawBytes[off], rawBytes[off + 3]);
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
        catch
        {
            return null;
        }
    }

    private void DrawPlayIcon(SpriteBatch spriteBatch, GraphicsDevice device, Texture2D pixel, Rectangle rect, Color color)
    {
        EnsurePlayIconTexture(device);
        if (_playIconTexture != null)
        {
            int margin = 5;
            int left = rect.X + margin;
            int right = rect.Right - margin;
            int top = rect.Y + margin;
            int bottom = rect.Bottom - margin;
            int w = right - left;
            int h = bottom - top;
            if (w > 0 && h > 0)
            {
                var dest = new Rectangle(left + (w - PlayIconSize) / 2, top + (h - PlayIconSize) / 2, PlayIconSize, PlayIconSize);
                spriteBatch.Draw(_playIconTexture, dest, null, color, 0, Vector2.Zero, SpriteEffects.FlipHorizontally, 0);
            }
        }
        else
        {
            DrawPlayIconFallback(spriteBatch, pixel, rect, color);
        }
    }

    private static void DrawPlayIconFallback(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
    {
        int margin = 5;
        int left = rect.X + margin;
        int right = rect.Right - margin;
        int top = rect.Y + margin;
        int bottom = rect.Bottom - margin;
        int h = bottom - top;
        int w = right - left;
        if (w <= 0 || h <= 0) return;
        int maxW = Math.Max(2, (w * 55) / 100);
        int centerRow = (h - 1) / 2;
        int topHeight = Math.Max(1, centerRow);
        int bottomHeight = Math.Max(1, h - 1 - centerRow);
        int tipX = left + (w - maxW) / 2;
        int baseX = tipX + maxW;
        for (int row = 0; row < h; row++)
        {
            int width;
            if (row <= centerRow)
                width = (maxW * row) / topHeight;
            else
                width = (maxW * (h - 1 - row)) / bottomHeight;
            if (width <= 0) continue;
            spriteBatch.Draw(pixel, new Rectangle(baseX - width, top + row, width, 1), color);
        }
    }

    private static void DrawPauseIcon(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
    {
        int cx = rect.X + rect.Width / 2;
        int cy = rect.Y + rect.Height / 2;
        int barW = 2;
        int gap = 2;
        int barH = 8;
        int leftX = cx - gap / 2 - barW;
        int rightX = cx + gap / 2;
        int y = cy - barH / 2;
        spriteBatch.Draw(pixel, new Rectangle(leftX, y, barW, barH), color);
        spriteBatch.Draw(pixel, new Rectangle(rightX, y, barW, barH), color);
    }

    private static void DrawVolumeIcon(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
    {
        int x = rect.X;
        int y = rect.Y;
        // Speaker cone (trapezoid: narrow at back, wider at opening), centered vertically in 16x16
        spriteBatch.Draw(pixel, new Rectangle(x + 2, y + 5, 1, 1), color);
        spriteBatch.Draw(pixel, new Rectangle(x + 2, y + 6, 2, 1), color);
        spriteBatch.Draw(pixel, new Rectangle(x + 1, y + 7, 3, 1), color);
        spriteBatch.Draw(pixel, new Rectangle(x + 1, y + 8, 4, 1), color);
        spriteBatch.Draw(pixel, new Rectangle(x + 1, y + 9, 3, 1), color);
        spriteBatch.Draw(pixel, new Rectangle(x + 2, y + 10, 2, 1), color);
        spriteBatch.Draw(pixel, new Rectangle(x + 2, y + 11, 1, 1), color);
        // Sound waves ))  — three arcs, expanding to the right
        spriteBatch.Draw(pixel, new Rectangle(x + 6, y + 6, 1, 4), color);
        spriteBatch.Draw(pixel, new Rectangle(x + 8, y + 5, 1, 6), color);
        spriteBatch.Draw(pixel, new Rectangle(x + 10, y + 4, 1, 8), color);
    }
}
