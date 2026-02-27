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

    /// <summary>Palette of beautiful pastel rainbow colors for new tracks.</summary>
    private static readonly Color[] s_trackColorPalette =
    {
        new(255, 182, 193),  // pastel pink
        new(255, 218, 185),  // pastel peach
        new(255, 250, 205),  // pastel lemon
        new(189, 252, 201),  // pastel mint
        new(176, 224, 230),  // pastel powder blue
        new(204, 204, 255),  // pastel periwinkle
        new(218, 191, 255),  // pastel violet
        new(230, 200, 255),  // pastel lavender
        new(255, 200, 220),  // pastel rose
        new(200, 235, 255),  // pastel sky
        new(200, 255, 220),  // pastel seafoam
        new(255, 230, 210),  // pastel apricot
    };

    /// <summary>Returns a random color for a new track.</summary>
    public static Color GetRandomTrackColor() =>
        s_trackColorPalette[s_random.Next(s_trackColorPalette.Length)];

    public abstract string TrackTypeId { get; }
    public string DisplayName { get; set; } = "Event Track";
    public int Order { get; set; }
    /// <summary>Color used for this track's note blocks on the timeline.</summary>
    public Color TrackColor { get; set; } = new(255, 182, 193);
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
