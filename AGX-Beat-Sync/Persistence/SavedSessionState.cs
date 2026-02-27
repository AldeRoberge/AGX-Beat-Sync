namespace AGX_Beat_Sync.Persistence;

/// <summary>Serializable snapshot of project and transport for save/load.</summary>
public class SavedSessionState
{
    public string AudioFilePath { get; set; } = string.Empty;
    public float BPM { get; set; } = 120f;
    public int TimeSignatureNumerator { get; set; } = 4;
    public int TimeSignatureDenominator { get; set; } = 4;
    public double BeatOffsetSeconds { get; set; }
    public double CurrentTime { get; set; }
    public List<Core.AutomationTrack> AutomationTracks { get; set; } = new();
    public List<Core.EventTrackBase> EventTracks { get; set; } = new();
}
