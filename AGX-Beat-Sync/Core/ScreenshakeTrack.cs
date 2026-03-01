namespace AGX_Beat_Sync.Core;

/// <summary>
/// Event track that shakes the game view camera when events fire.
/// Amplitude and duration are shared for all events on this track.
/// </summary>
public class ScreenshakeTrack : EventTrackBase
{
    public override string TrackTypeId => "Screenshake";

    /// <summary>Shake intensity (world-space offset scale). Typical range ~0.05–0.5.</summary>
    public float Amplitude { get; set; } = 0.15f;

    /// <summary>How long the shake lasts in seconds.</summary>
    public float Duration { get; set; } = 0.3f;

    public ScreenshakeTrack()
    {
        DisplayName = "Screenshake";
    }
}
