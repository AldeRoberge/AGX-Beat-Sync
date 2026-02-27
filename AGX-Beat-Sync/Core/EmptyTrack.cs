namespace AGX_Beat_Sync.Core;

public class EmptyTrack : EventTrackBase
{
    public override string TrackTypeId => "Empty";

    public EmptyTrack()
    {
        DisplayName = "Empty";
    }
}
