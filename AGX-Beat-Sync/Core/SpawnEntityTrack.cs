using Microsoft.Xna.Framework;

namespace AGX_Beat_Sync.Core;

public class SpawnEntityTrack : IEventTrack
{
    public string TrackTypeId => "SpawnEntity";
    public string DisplayName { get; set; } = "Spawn Entity";
    public int Order { get; set; }

    public PositionMode PositionMode { get; set; }
    public Vector3 PositionAbsolute { get; set; }
    public Vector3 PositionRelative { get; set; }

    public RotationMode RotationMode { get; set; }
    public Vector3 RotationEuler { get; set; }

    public float Speed { get; set; } = 1f;
}
