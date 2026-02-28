using Microsoft.Xna.Framework;

namespace AGX_Beat_Sync.Core;

public class SpawnEntityTrack : EventTrackBase
{
    public override string TrackTypeId => "SpawnEntity";

    public SpawnEntityTrack()
    {
        DisplayName = "Spawn Entity";
    }

    // --- Universal (every spawn event) ---
    /// <summary>Kind of entity to spawn. Projectiles use Speed; small cubes are stationary.</summary>
    public SpawnEntityKind EntityKind { get; set; } = SpawnEntityKind.Projectile;

    public PositionMode PositionMode { get; set; }
    public Vector3 PositionAbsolute { get; set; }
    public Vector3 PositionRelative { get; set; }

    public RotationMode RotationMode { get; set; } = RotationMode.Towards;
    public Vector3 RotationEuler { get; set; }

    public float Speed { get; set; } = 1.8f;
    /// <summary>Lifetime in seconds before the bullet is removed.</summary>
    public float Lifetime { get; set; } = 5f;

    // --- Projectile direction pattern ---
    public ProjectileDirectionPattern DirectionPattern { get; set; } = ProjectileDirectionPattern.Linear;
    /// <summary>Oscillation: wobble amplitude in degrees (only when DirectionPattern is Oscillation).</summary>
    public float OscillationAmplitude { get; set; } = 15f;
    /// <summary>Orbiting: radius of the orbit in world units (only when DirectionPattern is Orbiting).</summary>
    public float OrbitingDistance { get; set; } = 2f;

    // --- Mode: Single = 1 bullet, Multiple = N bullets ---
    public SpawnMode SpawnMode { get; set; } = SpawnMode.Single;
    /// <summary>Number of bullets when SpawnMode is Multiple (1–10).</summary>
    public int Count { get; set; } = 3;

    // --- Pattern (only when Multiple) ---
    public SpawnPattern Pattern { get; set; } = SpawnPattern.Circle;

    // Circle: radial burst
    public float CircleRadius { get; set; } = 0.5f;
    public bool CircleFullCircle { get; set; } = true;
    /// <summary>Spread angle in degrees when not full circle.</summary>
    public float CircleSpread { get; set; } = 90f;

    // Cone: forward spread
    /// <summary>Spread angle in degrees (fan).</summary>
    public float ConeSpreadAngle { get; set; } = 45f;

    // Line: bullet wall
    public float LineLength { get; set; } = 2f;
}
