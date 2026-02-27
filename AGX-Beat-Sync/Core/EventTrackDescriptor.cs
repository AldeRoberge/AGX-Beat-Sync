namespace AGX_Beat_Sync.Core;

/// <summary>
/// Describes a registered event track type for the dropdown and creation.
/// </summary>
public class EventTrackDescriptor
{
    public string TrackTypeId { get; }
    public string DisplayName { get; }
    public Func<EventTrackBase> Factory { get; }

    public EventTrackDescriptor(string trackTypeId, string displayName, Func<EventTrackBase> factory)
    {
        TrackTypeId = trackTypeId;
        DisplayName = displayName;
        Factory = factory;
    }
}
