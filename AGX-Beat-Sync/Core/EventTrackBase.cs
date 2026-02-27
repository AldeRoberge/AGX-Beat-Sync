using System.Text.Json.Serialization;

namespace AGX_Beat_Sync.Core;

/// <summary>
/// Default duration in seconds for new events (Ableton/FL-style note length when placing).
/// </summary>
public static class EventTrackConstants
{
    public const double DefaultEventDurationSeconds = 0.25;
}

/// <summary>
/// Base class for serializable event tracks. Provides EventTimes and common properties for the unified timeline.
/// Events can have a duration (e.g. for shortening/lengthening notes like in Ableton/FL).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SpawnEntityTrack), typeDiscriminator: "SpawnEntity")]
public abstract class EventTrackBase : IEventTrack
{
    public abstract string TrackTypeId { get; }
    public string DisplayName { get; set; } = "Event Track";
    public int Order { get; set; }
    public List<double> EventTimes { get; set; } = new();

    /// <summary>Duration in seconds per event time. Missing key = use DefaultEventDurationSeconds.</summary>
    public Dictionary<double, double> EventDurations { get; set; } = new();

    IList<double> IEventTrack.EventTimes => EventTimes;

    /// <summary>Duration of the event at the given start time. Returns default when not customized.</summary>
    public double GetDuration(double eventTime) =>
        EventDurations.TryGetValue(eventTime, out var d) ? d : EventTrackConstants.DefaultEventDurationSeconds;

    /// <summary>Set duration for an event (e.g. when resizing by dragging the note end).</summary>
    public void SetDuration(double eventTime, double duration)
    {
        if (Math.Abs(duration - EventTrackConstants.DefaultEventDurationSeconds) < 0.0001)
            EventDurations.Remove(eventTime);
        else
            EventDurations[eventTime] = duration;
    }
}
