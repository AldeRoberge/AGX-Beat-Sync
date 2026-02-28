using AGX_Beat_Sync.Audio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.Editor;

/// <summary>
/// Renders a centered waveform behind the piano roll: zero line in the middle,
/// negative peak above, positive below. Compressed and semi-transparent by default.
/// </summary>
public static class WaveformRenderer
{
    /// <summary>Visual style for the waveform strip. Override for custom look.</summary>
    public static WaveformStyle Style { get; set; } = WaveformStyle.Default;

    public static void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle stripBounds,
        WaveformCache waveform,
        TimelineViewState viewState,
        int contentX)
    {
        if (waveform.BucketCount == 0 || waveform.DurationSeconds <= 0)
            return;

        DrawBackground(spriteBatch, pixel, stripBounds);
        int centerY = stripBounds.Y + stripBounds.Height / 2;
        int halfH = (int)((stripBounds.Height / 2f - 2) * Style.VerticalScale);

        float maxPeak = ComputeVisibleMaxPeak(waveform, viewState, stripBounds, contentX);
        DrawCenterLine(spriteBatch, pixel, stripBounds, centerY);
        DrawPeakBars(spriteBatch, pixel, stripBounds, waveform, viewState, contentX, centerY, halfH, maxPeak);
    }

    private static void DrawBackground(SpriteBatch spriteBatch, Texture2D pixel, Rectangle stripBounds)
    {
        spriteBatch.Draw(pixel, stripBounds, Style.Background);
    }

    private static float ComputeVisibleMaxPeak(
        WaveformCache waveform,
        TimelineViewState viewState,
        Rectangle stripBounds,
        int contentX)
    {
        float maxPeak = 0.001f;
        for (int x = 0; x < stripBounds.Width; x++)
        {
            double time = viewState.ScreenToTime(stripBounds.X + x, contentX);
            if (time < 0 || time > waveform.DurationSeconds) continue;
            var (min, max) = waveform.GetPeakAtTime(time);
            float peak = Math.Max(Math.Abs(min), max);
            if (peak > maxPeak) maxPeak = peak;
        }
        return maxPeak < 0.0001f ? 1f : maxPeak;
    }

    private static void DrawCenterLine(SpriteBatch spriteBatch, Texture2D pixel, Rectangle stripBounds, int centerY)
    {
        spriteBatch.Draw(pixel, new Rectangle(stripBounds.X, centerY, stripBounds.Width, 1), Style.CenterLine);
    }

    private static void DrawPeakBars(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle stripBounds,
        WaveformCache waveform,
        TimelineViewState viewState,
        int contentX,
        int centerY,
        int halfH,
        float maxPeak)
    {
        for (int x = 0; x < stripBounds.Width; x++)
        {
            double time = viewState.ScreenToTime(stripBounds.X + x, contentX);
            if (time < 0 || time > waveform.DurationSeconds) continue;
            var (min, max) = waveform.GetPeakAtTime(time);

            float topOffset = (min / maxPeak) * halfH;
            float bottomOffset = (max / maxPeak) * halfH;
            int yTop = centerY + (int)topOffset;
            int yBottom = centerY + (int)bottomOffset;
            if (yTop > yBottom) (yTop, yBottom) = (yBottom, yTop);
            yTop = Math.Clamp(yTop, stripBounds.Y, stripBounds.Bottom);
            yBottom = Math.Clamp(yBottom, stripBounds.Y, stripBounds.Bottom);
            int h = Math.Max(1, yBottom - yTop);
            spriteBatch.Draw(pixel, new Rectangle(stripBounds.X + x, yTop, 1, h), Style.Fill);
        }
    }

    /// <summary>Colors and scale for waveform drawing.</summary>
    public struct WaveformStyle
    {
        public Color Background;
        public Color CenterLine;
        public Color Fill;
        /// <summary>Fraction of half-height used (e.g. 0.4 = 40%).</summary>
        public float VerticalScale;

        public static WaveformStyle Default => new()
        {
            // Darker waveform so it reads clearly without overpowering notes/grid
            Background = new Color(18, 20, 26, 100),
            CenterLine = new Color(36, 40, 48, 90),
            Fill = new Color(48, 88, 120, 130),
            VerticalScale = 0.4f
        };
    }
}
