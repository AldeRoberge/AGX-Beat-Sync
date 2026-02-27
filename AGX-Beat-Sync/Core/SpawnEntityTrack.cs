using Microsoft.Xna.Framework;

namespace AGX_Beat_Sync.Core;

public class SpawnEntityTrack : EventTrackBase
{
    public override string TrackTypeId => "SpawnEntity";

    public SpawnEntityTrack()
    {
        DisplayName = "Spawn Entity";
    }

    public PositionMode PositionMode { get; set; }
    public Vector3 PositionAbsolute { get; set; }
    public Vector3 PositionRelative { get; set; }

    public RotationMode RotationMode { get; set; }
    public Vector3 RotationEuler { get; set; }

    public float Speed { get; set; } = 1f;
}
