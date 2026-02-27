namespace AGX_Beat_Sync.Core;

public class Project
{
    public string AudioFilePath { get; set; } = string.Empty;
    public float BPM { get; set; } = 120f;
    public int TimeSignatureNumerator { get; set; } = 4;
    public int TimeSignatureDenominator { get; set; } = 4;
    /// <summary>Time in seconds where the first beat (beat 0) of the grid sits in the song. Increase to shift the grid right so playback aligns on beat.</summary>
    public double BeatOffsetSeconds { get; set; }
    /// <summary>Song in point (seconds). Set with I key. Null = not set.</summary>
    public double? InTime { get; set; }
    /// <summary>Song out point (seconds). Set with O key. Null = not set.</summary>
    public double? OutTime { get; set; }
    public List<AutomationTrack> AutomationTracks { get; set; } = new();
    /// <summary>Unified event tracks: each track has event times and drives gameplay (e.g. Spawn Entity at those times).</summary>
    public List<EventTrackBase> EventTracks { get; set; } = new();
}
