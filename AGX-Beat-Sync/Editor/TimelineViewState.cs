namespace AGX_Beat_Sync.Editor;

/// <summary>
/// Timeline coordinate system: time &lt;-&gt; screen.
/// ScreenX = (Time - ViewStartTime) * Zoom  (relative to content left)
/// Time = ScreenX / Zoom + ViewStartTime
/// </summary>
public class TimelineViewState
{
    /// <summary>Left edge of the visible time range, in seconds.</summary>
    public double ViewStartTime { get; set; }

    /// <summary>Pixels per second of time.</summary>
    public float Zoom { get; set; } = 80f;

    /// <summary>Grid subdivisions per beat (1=whole beat, 2=1/2, 4=1/4, 8=1/8, etc.). Used for grid lines and snap. Ableton-style: Ctrl+1 finer, Ctrl+2 coarser.</summary>
    public int GridSubdivisionsPerBeat { get; set; } = 4;

    public const int MinGridSubdivisions = 1;
    public const int MaxGridSubdivisions = 32;

    public float MinZoom => 20f;
    public float MaxZoom => 800f;

    /// <summary>Convert time (seconds) to screen X relative to content area. Content origin is at 0.</summary>
    public float TimeToScreen(double time, int contentX)
    {
        return (float)((time - ViewStartTime) * Zoom) + contentX;
    }

    /// <summary>Convert screen X to time (seconds).</summary>
    public double ScreenToTime(float screenX, int contentX)
    {
        return (screenX - contentX) / Zoom + ViewStartTime;
    }

    /// <summary>Visible time range end (start + content width in time).</summary>
    public double ViewEndTime(int contentWidth)
    {
        return ViewStartTime + contentWidth / (double)Zoom;
    }

    public void Pan(float deltaPixels)
    {
        ViewStartTime -= deltaPixels / (double)Zoom;
        ViewStartTime = Math.Max(0, ViewStartTime);
    }

    public void ZoomAt(float deltaZoom, float screenX, int contentX)
    {
        double timeAtCursor = ScreenToTime(screenX, contentX);
        Zoom = Math.Clamp(Zoom + deltaZoom, MinZoom, MaxZoom);
        // Keep the time under cursor fixed
        ViewStartTime = timeAtCursor - (screenX - contentX) / (double)Zoom;
        ViewStartTime = Math.Max(0, ViewStartTime);
    }
}
