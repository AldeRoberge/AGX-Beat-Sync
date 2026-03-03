namespace AGX_Beat_Sync.Core;

/// <summary>Tile shape options for the Change Tiles event.</summary>
public enum ChangeTilesShape
{
    Circle,
    Square,
    Line,
    Cone,
}

public class ChangeTilesTrack : EventTrackBase
{
    public override string TrackTypeId => "ChangeTiles";

    public ChangeTilesTrack()
    {
        DisplayName = "Change Tiles";
    }

    /// <summary>Per-event shape. Missing key = use default (Circle).</summary>
    public Dictionary<double, ChangeTilesShape> EventShapes { get; set; } = new();

    /// <summary>Shape for the event at the given time. Returns default when not set.</summary>
    public ChangeTilesShape GetShape(double eventTime)
    {
        return EventShapes.TryGetValue(eventTime, out var s) ? s : ChangeTilesShape.Circle;
    }

    /// <summary>Set shape for an event at the given time.</summary>
    public void SetShape(double eventTime, ChangeTilesShape shape)
    {
        EventShapes[eventTime] = shape;
    }
}
