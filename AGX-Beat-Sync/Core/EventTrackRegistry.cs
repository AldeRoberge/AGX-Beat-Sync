namespace AGX_Beat_Sync.Core;

/// <summary>
/// Registry of event track types. New track types register here so the track list can create them without tight coupling.
/// Inspector renderers are registered separately in Editor.InspectorRendererRegistry.
/// </summary>
public static class EventTrackRegistry
{
    private static readonly List<EventTrackDescriptor> s_descriptors = new();

    public static IReadOnlyList<EventTrackDescriptor> AllTypes => s_descriptors;

    public static void Register(EventTrackDescriptor descriptor)
    {
        s_descriptors.Add(descriptor);
    }

    public static IEventTrack CreateTrack(string trackTypeId)
    {
        var d = s_descriptors.FirstOrDefault(x => x.TrackTypeId == trackTypeId);
        if (d == null)
            throw new ArgumentException($"Unknown track type: {trackTypeId}", nameof(trackTypeId));
        return d.Factory();
    }
}
