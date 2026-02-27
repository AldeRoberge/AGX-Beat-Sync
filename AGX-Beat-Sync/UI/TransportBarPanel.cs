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
    private const int BpmAreaWidth = 120;
    private const int TimeAreaWidth = 140;
    private const int Padding = 8;
    private const int VolumeSliderWidth = 88;
    private const int SliderTrackHeight = 16;

    private float _volume = 1f;
    private bool _volumeSliderDragging;

    /// <summary>Master volume (0.0 to 1.0). Synced to audio playback by the game.</summary>
    public float Volume { get => _volume; set => _volume = Math.Clamp(value, 0f, 1f); }

    private Texture2D? _bpmTextTexture;
    private string _bpmCachedString = "";
    private int _bpmCachedWidth = -1;
    private int _bpmCachedHeight = -1;
    private Texture2D? _timeTextTexture;
    private string _timeCachedString = "";
    private int _timeCachedWidth = -1;
    private int _timeCachedHeight = -1;

    public Project? Project { get; set; }
    public Transport? Transport { get; set; }
    public Input.InputManager? Input { get; set; }

    public TransportBarPanel()
    {
        Title = "Transport";
        HeaderHeight = 32;
        BackgroundColor = new Color(45, 48, 52);
        HeaderColor = new Color(45, 48, 52);
    }

    private Rectangle GetVolumeSliderTrack()
    {
        int x = Bounds.X + Padding;
        int y = Bounds.Y + (Bounds.Height - SliderTrackHeight) / 2;
        return new Rectangle(x, y, VolumeSliderWidth - Padding, SliderTrackHeight);
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

    private Rectangle GetTimeAreaRect()
    {
        var bpm = GetBpmArea();
        int x = bpm.Right + Padding;
        int y = Bounds.Y + (Bounds.Height - 24) / 2;
        return new Rectangle(x, y, TimeAreaWidth, 24);
    }

    public override string? GetHoverText(Point mouse)
    {
        if (!ContainsPoint(mouse)) return null;
        var volTrack = GetVolumeSliderTrack();
        if (volTrack.Contains(mouse)) return "Volume";
        var minus = GetBpmMinusButton();
        if (minus.Contains(mouse)) return "BPM −";
        var plus = GetBpmPlusButton();
        if (plus.Contains(mouse)) return "BPM +";
        if (GetBpmValueRect().Contains(mouse)) return "BPM — click to edit";
        if (GetTimeAreaRect().Contains(mouse)) return "Time — click to go to position";
        return "Transport";
    }

    public override void Update(GameTime gameTime)
    {
        if (Input == null) return;

        var volTrack = GetVolumeSliderTrack();
        if (Input.MouseLeftPressed && volTrack.Contains(Input.MousePosition))
            _volumeSliderDragging = true;
        if (Input.MouseLeftReleased)
            _volumeSliderDragging = false;

        if (_volumeSliderDragging && volTrack.Width > 0)
        {
            int mx = Math.Clamp(Input.MousePosition.X, volTrack.X, volTrack.Right);
            Volume = (float)(mx - volTrack.X) / volTrack.Width;
        }

        if (Transport == null || Project == null) return;
        if (!Input.MouseLeftPressed) return;
        if (!ContainsPoint(Input.MousePosition)) return;

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

        // Volume slider (track + fill + thumb)
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
}
