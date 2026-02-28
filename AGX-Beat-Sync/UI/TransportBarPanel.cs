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
    private bool _progressBarDragging;
    /// <summary>Game time until which to show "Volume: N%" in the status bar (set while dragging).</summary>
    private double _volumeStatusVisibleUntil;
    /// <summary>Total game time in seconds, updated in Update; used for REC button blink when armed but not playing.</summary>
    private double _totalGameTimeSeconds;

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
    private Texture2D? _circleOutlineTexture;
    private Texture2D? _playIconTexture;
    private Texture2D? _metronomeTextTexture;
    private const int PlayIconSize = 14;

    public Project? Project { get; set; }
    public Transport? Transport { get; set; }
    /// <summary>Total duration in seconds for progress bar (e.g. from waveform). When 0 or null, bar is empty.</summary>
    public double TotalDurationSeconds { get; set; }
    public Input.InputManager? Input { get; set; }
    /// <summary>When true, show REC indicator; keys 1-9 and 0 add events to tracks while playing.</summary>
    public bool RecordMode { get; set; }
    /// <summary>When true, metronome is on (tock on beat 1, tick on beats 2–4).</summary>
    public bool MetronomeOn { get; set; }

    public TransportBarPanel()
    {
        Title = "Transport";
        HeaderHeight = 32;
        BackgroundColor = new Color(45, 48, 52);
        HeaderColor = new Color(45, 48, 52);
    }

    private const int ContentRowHeight = 32;

    private Rectangle GetVolumeIconRect()
    {
        int x = Bounds.X + Padding;
        int y = Bounds.Y + (ContentRowHeight - VolumeIconSize) / 2;
        return new Rectangle(x, y, VolumeIconSize, VolumeIconSize);
    }

    private Rectangle GetVolumeSliderTrack()
    {
        var icon = GetVolumeIconRect();
        int x = icon.Right + 4;
        int y = Bounds.Y + (ContentRowHeight - SliderTrackHeight) / 2;
        int width = VolumeSliderWidth - (icon.Width + 4 + Padding);
        return new Rectangle(x, y, Math.Max(0, width), SliderTrackHeight);
    }

    private Rectangle GetBpmArea()
    {
        int x = Bounds.X + Padding + VolumeSliderWidth;
        int y = Bounds.Y + (ContentRowHeight - 24) / 2;
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
    /// <summary>Invoked when user clicks the Metronome button (toggle metronome on/off).</summary>
    public Action? OnMetronomeToggle { get; set; }
    /// <summary>Invoked when user seeks (e.g. by dragging the progress bar). Parameter is time in seconds.</summary>
    public Action<double>? SeekRequested { get; set; }

    private const int MetronomeButtonSize = 20;

    private Rectangle GetMetronomeButtonRect()
    {
        var bpm = GetBpmArea();
        int x = bpm.Right + Padding;
        int y = Bounds.Y + (ContentRowHeight - MetronomeButtonSize) / 2;
        return new Rectangle(x, y, MetronomeButtonSize, MetronomeButtonSize);
    }

    private Rectangle GetPlayPauseButtonRect()
    {
        var metro = GetMetronomeButtonRect();
        int x = metro.Right + Padding;
        int y = Bounds.Y + (ContentRowHeight - 24) / 2;
        return new Rectangle(x, y, PlayPauseButtonWidth, 24);
    }

    private const int RecButtonSize = 20;
    private const int RecButtonOutlinePadding = 1;

    private Rectangle GetRecButtonRect()
    {
        var playPause = GetPlayPauseButtonRect();
        int x = playPause.Right + Padding;
        int y = Bounds.Y + (ContentRowHeight - RecButtonSize) / 2;
        return new Rectangle(x, y, RecButtonSize, RecButtonSize);
    }

    private const int ProgressBarHeight = 4;

    private Rectangle GetTimeAreaRect()
    {
        var rec = GetRecButtonRect();
        int x = rec.Right + Padding;
        int y = Bounds.Y + (32 - 24) / 2; // keep time area in top 32px row
        return new Rectangle(x, y, TimeAreaWidth, 24);
    }

    private Rectangle GetProgressBarRect()
    {
        var timeArea = GetTimeAreaRect();
        int y = Bounds.Y + 30; // just under time area (time area bottom = 28 in 32px row)
        return new Rectangle(timeArea.X, y, timeArea.Width, ProgressBarHeight);
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
        if (GetMetronomeButtonRect().Contains(mouse)) return MetronomeOn ? "Metronome on — tock on beat 1, tick on beats 2–4" : "Metronome off — click to enable";
        if (GetTimeAreaRect().Contains(mouse)) return "Time — click to go to position";
        if (GetProgressBarRect().Contains(mouse)) return "Drag to move playhead";
        return "Transport";
    }

    public override void Update(GameTime gameTime)
    {
        _totalGameTimeSeconds = gameTime.TotalGameTime.TotalSeconds;
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

        if (Input.MouseLeftReleased)
            _progressBarDragging = false;

        if (_progressBarDragging && TotalDurationSeconds > 0 && SeekRequested != null)
        {
            var bar = GetProgressBarRect();
            int mx = Math.Clamp(Input.MousePosition.X, bar.X, bar.Right);
            double t = bar.Width > 0 ? (double)(mx - bar.X) / bar.Width * TotalDurationSeconds : 0;
            t = Math.Clamp(t, 0, TotalDurationSeconds);
            SeekRequested(t);
        }
        else if (Input.MouseLeftPressed && GetProgressBarRect().Contains(Input.MousePosition) && TotalDurationSeconds > 0 && SeekRequested != null)
        {
            _progressBarDragging = true;
            var bar = GetProgressBarRect();
            int mx = Math.Clamp(Input.MousePosition.X, bar.X, bar.Right);
            double t = bar.Width > 0 ? (double)(mx - bar.X) / bar.Width * TotalDurationSeconds : 0;
            t = Math.Clamp(t, 0, TotalDurationSeconds);
            SeekRequested(t);
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
        if (GetMetronomeButtonRect().Contains(Input.MousePosition))
        {
            OnMetronomeToggle?.Invoke();
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

        // REC circle button: gray when off; when record mode on: solid red when playing, blink when armed (not playing)
        var recRect = GetRecButtonRect();
        bool recHover = recRect.Contains(Input?.MousePosition ?? Point.Zero);
        EnsureCircleTexture(spriteBatch.GraphicsDevice);
        Color recColor;
        if (!RecordMode)
            recColor = recHover ? new Color(90, 94, 100) : new Color(60, 64, 70);
        else if (Transport?.IsPlaying == true)
            recColor = new Color(200, 50, 50); // solid when playing
        else
        {
            // armed but not playing: blink (toggle every ~0.5s)
            bool blinkOn = ((int)(_totalGameTimeSeconds / 0.5) % 2) == 0;
            recColor = blinkOn ? new Color(200, 50, 50) : new Color(80, 25, 25);
        }
        Color outlineColor = new Color(70, 74, 82);
        if (_circleOutlineTexture != null)
        {
            var outlineRect = new Rectangle(recRect.X - RecButtonOutlinePadding, recRect.Y - RecButtonOutlinePadding,
                RecButtonSize + 2 * RecButtonOutlinePadding, RecButtonSize + 2 * RecButtonOutlinePadding);
            spriteBatch.Draw(_circleOutlineTexture, outlineRect, outlineColor);
        }
        if (_circleTexture != null)
            spriteBatch.Draw(_circleTexture, recRect, recColor);

        // Metronome button: M (tock on beat 1, tick on 2–4)
        var metroRect = GetMetronomeButtonRect();
        bool metroHover = metroRect.Contains(Input?.MousePosition ?? Point.Zero);
        Color metroBg = MetronomeOn ? (metroHover ? new Color(70, 90, 120) : new Color(55, 75, 100)) : (metroHover ? new Color(55, 58, 65) : new Color(48, 52, 58));
        spriteBatch.Draw(pixel, metroRect, metroBg);
        EnsureMetronomeTextTexture(spriteBatch.GraphicsDevice);
        if (_metronomeTextTexture != null)
        {
            int mx = metroRect.X + (metroRect.Width - _metronomeTextTexture.Width) / 2;
            int my = metroRect.Y + (metroRect.Height - _metronomeTextTexture.Height) / 2;
            spriteBatch.Draw(_metronomeTextTexture, new Rectangle(mx, my, _metronomeTextTexture.Width, _metronomeTextTexture.Height), new Color(200, 202, 208));
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

        // Progress bar under timecode: how far we've gone in the music (blue on dark)
        var progressBar = GetProgressBarRect();
        spriteBatch.Draw(pixel, progressBar, new Color(38, 41, 46));
        if (TotalDurationSeconds > 0 && Transport != null)
        {
            float t = (float)Math.Clamp(Transport.CurrentTime / TotalDurationSeconds, 0, 1);
            int fillW = (int)(progressBar.Width * t);
            if (fillW > 0)
                spriteBatch.Draw(pixel, new Rectangle(progressBar.X, progressBar.Y, fillW, progressBar.Height), new Color(90, 140, 200));
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
        _circleOutlineTexture?.Dispose();
        _circleTexture = CreateCircleTexture(device, RecButtonSize, softEdge: true);
        _circleOutlineTexture = CreateCircleRingTexture(device, RecButtonSize + 2 * RecButtonOutlinePadding, 1f);
    }

    private void EnsureMetronomeTextTexture(GraphicsDevice device)
    {
        if (_metronomeTextTexture != null && !_metronomeTextTexture.IsDisposed) return;
        _metronomeTextTexture?.Dispose();
        var metroRect = GetMetronomeButtonRect();
        _metronomeTextTexture = CreateLabelTextTexture(device, "M", Math.Max(1, metroRect.Width), Math.Max(1, metroRect.Height));
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
        // Build right-pointing triangle in texture (base at x=0, tip at x=maxW). Drawn with SpriteEffects.None.
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

    private static Texture2D? CreateCircleTexture(GraphicsDevice device, int size, bool softEdge = false)
    {
        var data = new Microsoft.Xna.Framework.Color[size * size];
        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float r = cx - 0.5f;
        const float softWidth = 1.2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                byte alpha;
                if (softEdge)
                {
                    if (dist <= r - 0.5f) alpha = 255;
                    else if (dist >= r + softWidth) alpha = 0;
                    else alpha = (byte)Math.Clamp((int)(255 * (r + softWidth - dist) / (softWidth + 0.5f)), 0, 255);
                }
                else
                {
                    alpha = (dist <= r) ? (byte)255 : (byte)0;
                }
                // Use fully transparent (0,0,0,0) outside circle so no square background shows; white only where visible
                data[y * size + x] = alpha == 0 ? Microsoft.Xna.Framework.Color.Transparent : new Microsoft.Xna.Framework.Color((byte)255, (byte)255, (byte)255, alpha);
            }
        }
        var tex = new Texture2D(device, size, size, false, SurfaceFormat.Color);
        tex.SetData(data);
        return tex;
    }

    /// <summary>Creates a round ring (hollow circle) texture for outline. Ring is at the outer edge of the circle.</summary>
    private static Texture2D? CreateCircleRingTexture(GraphicsDevice device, int size, float ringWidth)
    {
        var data = new Microsoft.Xna.Framework.Color[size * size];
        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float rOuter = cx - 0.5f;
        float rInner = Math.Max(0, rOuter - ringWidth);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                byte alpha = 0;
                if (dist >= rInner && dist <= rOuter + 0.6f)
                {
                    if (dist <= rInner + 0.5f)
                        alpha = (byte)Math.Clamp((int)(255 * (dist - rInner) / 0.5f), 0, 255);
                    else if (dist >= rOuter - 0.5f)
                        alpha = (byte)Math.Clamp((int)(255 * (rOuter + 0.6f - dist) / 1.1f), 0, 255);
                    else
                        alpha = 255;
                }
                // Use fully transparent outside the ring so no square background shows
                data[y * size + x] = alpha == 0 ? Microsoft.Xna.Framework.Color.Transparent : new Microsoft.Xna.Framework.Color((byte)255, (byte)255, (byte)255, alpha);
            }
        }
        var tex = new Texture2D(device, size, size, false, SurfaceFormat.Color);
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
                var dest = new Rectangle(left + (w - PlayIconSize) / 2 + 2, top + (h - PlayIconSize) / 2, PlayIconSize, PlayIconSize);
                spriteBatch.Draw(_playIconTexture, dest, null, color, 0, Vector2.Zero, SpriteEffects.None, 0);
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
        int tipX = left + (w - maxW) / 2 + 2;
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
        int cx = rect.X + rect.Width / 2 + 2;
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
