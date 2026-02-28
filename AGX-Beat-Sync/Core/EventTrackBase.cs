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
[JsonDerivedType(typeof(ChangeEntityColorTrack), typeDiscriminator: "ChangeEntityColor")]
public abstract class EventTrackBase : IEventTrack
{
    private static readonly Random s_random = new();

    /// <summary>Vivid track colors (green, blue, cyan, pink, purple) — no red/yellow. Used with white note textures so these show true.</summary>
    private static readonly Color[] s_trackColorPalette =
    {
        new(80, 220, 130),   // green
        new(100, 255, 150),   // bright green
        new(0, 200, 180),     // teal
        new(60, 200, 255),    // sky blue
        new(100, 160, 255),   // blue
        new(140, 120, 255),   // periwinkle
        new(200, 100, 255),   // violet
        new(255, 100, 220),   // magenta / hot pink
        new(255, 110, 180),   // pink
        new(255, 130, 200),   // rose pink
        new(0, 230, 220),     // cyan
        new(120, 230, 255),   // light cyan
        new(180, 255, 120),   // lime
        new(160, 220, 255),   // ice blue
    };

    /// <summary>Returns a random color for a new track.</summary>
    public static Color GetRandomTrackColor() =>
        s_trackColorPalette[s_random.Next(s_trackColorPalette.Length)];

    public abstract string TrackTypeId { get; }
    public string DisplayName { get; set; } = "Event Track";
    public int Order { get; set; }
    /// <summary>Color used for this track's note blocks on the timeline.</summary>
    public Color TrackColor { get; set; } = new(100, 200, 255);
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
