using AGX_Beat_Sync.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.Editor;

/// <summary>
/// Renders beat grid: measure lines, beat lines, subdivision lines.
/// </summary>
public static class TimelineGridRenderer
{
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
        double viewEnd = viewState.ViewEndTime(contentBounds.Width);

        // Subdivision step in seconds (e.g. 1/4 beat)
        double subStep = secondsPerBeat / subDiv;
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

            // FL Studio-style grid: measure = strongest, beat = medium, subdivision = very subtle
            Color color;
            int thickness = 1;
            double measureF = (t - beatOffsetSeconds) / measureStep;
            double beatF = (t - beatOffsetSeconds) / beatStep;
            if (Math.Abs(measureF - Math.Round(measureF)) < Eps)
            {
                color = new Color(58, 62, 72);
                thickness = 2;
            }
            else if (Math.Abs(beatF - Math.Round(beatF)) < Eps)
            {
                color = new Color(42, 46, 54);
            }
            else
            {
                color = new Color(34, 37, 43);
            }

            for (int i = 0; i < thickness; i++)
                spriteBatch.Draw(pixel, new Rectangle((int)x + i, contentBounds.Y, 1, contentBounds.Height), color);
        }
    }
}
