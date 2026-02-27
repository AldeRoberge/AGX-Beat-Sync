namespace AGX_Beat_Sync.Core;

public class Project
{
    public string AudioFilePath { get; set; } = string.Empty;
    public float BPM { get; set; } = 120f;
    public int TimeSignatureNumerator { get; set; } = 4;
    public int TimeSignatureDenominator { get; set; } = 4;
    /// <summary>Song in point (seconds). Set with I key. Beat grid and snap use this as beat 0 when set. Null = not set.</summary>
    public double? InTime { get; set; }
    /// <summary>Song out point (seconds). Set with O key. Null = not set.</summary>
    public double? OutTime { get; set; }
    public List<AutomationTrack> AutomationTracks { get; set; } = new();
    /// <summary>Unified event tracks: each track has event times and drives gameplay (e.g. Spawn Entity at those times).</summary>
    public List<EventTrackBase> EventTracks { get; set; } = new();
}
