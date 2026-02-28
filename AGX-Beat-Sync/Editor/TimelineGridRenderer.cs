using AGX_Beat_Sync.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.Editor;

/// <summary>
/// Renders beat grid: measure lines, beat lines, subdivision lines.
/// When zoomed out, progressively coarsens the drawn grid so fine lines don't hide the waveform.
/// </summary>
public static class TimelineGridRenderer
{
    /// <summary>Minimum pixels between grid lines; below this we draw a coarser grid so the waveform stays visible.</summary>
    private const double MinGridLinePixelSpacing = 10.0;

    public static void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle contentBounds,
        TimelineViewState viewState,
        double bpm,
        int timeSigNum,
        int timeSigDenom,
        double beatOffsetSeconds = 0)
    {
        int subDiv = Math.Clamp(viewState.GridSubdivisionsPerBeat, 1, 64);
        double secondsPerBeat = 60.0 / bpm;
        // When zoomed out, reduce drawn subdivisions so grid doesn't clutter the waveform
        double maxSubDivByZoom = secondsPerBeat * viewState.Zoom / MinGridLinePixelSpacing;
        int effectiveSubDiv = Math.Clamp((int)Math.Max(1, maxSubDivByZoom), 1, subDiv);
        double viewEnd = viewState.ViewEndTime(contentBounds.Width);

        // Subdivision step in seconds (effectiveSubDiv may be coarser when zoomed out)
        double subStep = secondsPerBeat / effectiveSubDiv;
        double beatStep = secondsPerBeat;
        double measureStep = secondsPerBeat * timeSigNum; // measure = numerator beats

        // Align first line to grid (grid starts at beatOffsetSeconds)
        double t0 = beatOffsetSeconds + Math.Floor((viewState.ViewStartTime - beatOffsetSeconds) / subStep) * subStep;
        if (t0 < viewState.ViewStartTime) t0 += subStep;

        const double Eps = 1e-9;
        for (double t = t0; t <= viewEnd; t += subStep)
        {
            float x = viewState.TimeToScreen(t, contentBounds.X);
            if (x < contentBounds.X - 1 || x > contentBounds.Right + 1) continue;

            // FL Studio-style grid: measure = strongest, beat = medium, subdivision = subtle but visible
            Color color;
            int thickness = 1;
            double measureF = (t - beatOffsetSeconds) / measureStep;
            double beatF = (t - beatOffsetSeconds) / beatStep;
            if (Math.Abs(measureF - Math.Round(measureF)) < Eps)
            {
                color = new Color(78, 84, 98);
                thickness = 2;
            }
            else if (Math.Abs(beatF - Math.Round(beatF)) < Eps)
            {
                color = new Color(62, 68, 80);
            }
            else
            {
                color = new Color(50, 54, 64);
            }

            for (int i = 0; i < thickness; i++)
                spriteBatch.Draw(pixel, new Rectangle((int)x + i, contentBounds.Y, 1, contentBounds.Height), color);
        }
    }
}
