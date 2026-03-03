using System.Text.Json.Serialization;

namespace AGX_Beat_Sync.Core;

/// <summary>Movement options for the Change Entity Movement event (enemy cube in game view).</summary>
public enum EntityMovementKind
{
    Stationary,
    Circle,
    Wandering,
    Chasing,
    Hovering,
}

public class ChangeEntityMovementTrack : EventTrackBase
{
    public override string TrackTypeId => "ChangeEntityMovement";

    public ChangeEntityMovementTrack()
    {
        DisplayName = "Change Entity Movement";
    }

    /// <summary>Per-event movement kind. Missing key = use default (Stationary).</summary>
    public Dictionary<double, EntityMovementKind> EventMovements { get; set; } = new();

    /// <summary>Movement kind for the event at the given time. Returns default when not set.</summary>
    public EntityMovementKind GetMovement(double eventTime)
    {
        return EventMovements.TryGetValue(eventTime, out var m) ? m : EntityMovementKind.Stationary;
    }

    /// <summary>Set movement kind for an event at the given time.</summary>
    public void SetMovement(double eventTime, EntityMovementKind movement)
    {
        EventMovements[eventTime] = movement;
    }
}
