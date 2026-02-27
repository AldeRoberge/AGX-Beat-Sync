namespace AGX_Beat_Sync.Core;

public class SfxTrack : EventTrackBase
{
    public override string TrackTypeId => "SFX";

    /// <summary>FMOD audio event path (e.g. "event:/SFX/Impact").</summary>
    public string FmodAudioEventPath { get; set; } = "";

    public SfxTrack()
    {
        DisplayName = "SFX";
    }
}
