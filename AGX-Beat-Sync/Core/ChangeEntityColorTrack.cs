using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace AGX_Beat_Sync.Core;

/// <summary>Color options for the Change Entity Color event (entity cube in game view).</summary>
public enum EntityColor
{
    Red,
    Green,
    Blue,
    Yellow,
    Cyan,
    Magenta,
    Orange,
    Purple,
    White,
}

public class ChangeEntityColorTrack : EventTrackBase
{
    public override string TrackTypeId => "ChangeEntityColor";

    public ChangeEntityColorTrack()
    {
        DisplayName = "Change Entity Color";
    }

    /// <summary>Per-event color. Missing key = use default (Red).</summary>
    public Dictionary<double, EntityColor> EventColors { get; set; } = new();

    /// <summary>Color for the event at the given time. Returns default when not set.</summary>
    public EntityColor GetColor(double eventTime)
    {
        return EventColors.TryGetValue(eventTime, out var c) ? c : EntityColor.Red;
    }

    /// <summary>Set color for an event at the given time.</summary>
    public void SetColor(double eventTime, EntityColor color)
    {
        EventColors[eventTime] = color;
    }

    /// <summary>Convert EntityColor enum to XNA Color for rendering.</summary>
    public static Color ToXnaColor(EntityColor c)
    {
        return c switch
        {
            EntityColor.Red => new Color(200, 80, 80),
            EntityColor.Green => new Color(80, 200, 100),
            EntityColor.Blue => new Color(80, 120, 220),
            EntityColor.Yellow => new Color(220, 220, 80),
            EntityColor.Cyan => new Color(80, 220, 220),
            EntityColor.Magenta => new Color(220, 80, 220),
            EntityColor.Orange => new Color(255, 160, 60),
            EntityColor.Purple => new Color(140, 80, 200),
            EntityColor.White => new Color(220, 220, 220),
            _ => new Color(200, 80, 80)
        };
    }
}
