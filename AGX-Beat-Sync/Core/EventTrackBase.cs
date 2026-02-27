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
[JsonDerivedType(typeof(EmptyTrack), typeDiscriminator: "Empty")]
[JsonDerivedType(typeof(SpawnEntityTrack), typeDiscriminator: "SpawnEntity")]
[JsonDerivedType(typeof(SfxTrack), typeDiscriminator: "SFX")]
public abstract class EventTrackBase : IEventTrack
{
    private static readonly Random s_random = new();

    /// <summary>Palette of distinct colors for new tracks (saturated, readable on dark UI).</summary>
    private static readonly Color[] s_trackColorPalette =
    {
        new(238, 145, 100),  // orange (original)
        new(100, 180, 220),  // blue
        new(120, 200, 120),  // green
        new(220, 120, 140),  // pink
        new(200, 160, 220),  // purple
        new(220, 200, 100),  // yellow
        new(100, 200, 200),  // cyan
        new(220, 140, 80),   // coral
        new(140, 180, 220),  // light blue
        new(180, 220, 140),  // lime
        new(220, 160, 180),  // rose
        new(160, 140, 220),  // violet
    };

    /// <summary>Returns a random color for a new track.</summary>
    public static Color GetRandomTrackColor() =>
        s_trackColorPalette[s_random.Next(s_trackColorPalette.Length)];

    public abstract string TrackTypeId { get; }
    public string DisplayName { get; set; } = "Event Track";
    public int Order { get; set; }
    /// <summary>Color used for this track's note blocks on the timeline.</summary>
    public Color TrackColor { get; set; } = new(238, 145, 100);
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
