namespace AGX_Beat_Sync.Persistence;

/// <summary>Serializable snapshot of project and transport for save/load.</summary>
public class SavedSessionState
{
    public string AudioFilePath { get; set; } = string.Empty;
    public float BPM { get; set; } = 120f;
    public int TimeSignatureNumerator { get; set; } = 4;
    public int TimeSignatureDenominator { get; set; } = 4;
    public double BeatOffsetSeconds { get; set; }
    public double? InTime { get; set; }
    public double? OutTime { get; set; }
    /// <summary>Playhead position in seconds. Saved and restored so timeline position is preserved.</summary>
    public double CurrentTime { get; set; }
    public List<Core.AutomationTrack> AutomationTracks { get; set; } = new();
    public List<Core.EventTrackBase> EventTracks { get; set; } = new();

    /// <summary>Timeline view: scroll position and zoom. Restored so "where you were" is preserved.</summary>
    public double ViewStartTime { get; set; }
    public float Zoom { get; set; } = 80f;
    public int GridSubdivisionsPerBeat { get; set; } = 4;

    /// <summary>Layout: game view panel height in pixels. Restored on launch so divider position is preserved.</summary>
    public int GameViewHeightPx { get; set; } = 120;

    /// <summary>Layout: game view panel width in pixels (bottom row). 0 = use default fraction. Restored on launch.</summary>
    public int GameViewWidthPx { get; set; }

    /// <summary>Layout: inspector panel width in pixels. 0 = use default (280). Restored on launch.</summary>
    public int InspectorWidthPx { get; set; }

    /// <summary>Game view camera: target (player) position. Restored on launch.</summary>
    public float CameraTargetX { get; set; }
    public float CameraTargetY { get; set; } = 0.5f;
    public float CameraTargetZ { get; set; }

    /// <summary>Game view camera: orbit angles (radians) and distance. Restored on launch.</summary>
    public float CameraOrbitYaw { get; set; } = -0.26f;
    public float CameraOrbitPitch { get; set; } = -0.644f;
    public float CameraOrbitDistance { get; set; } = 8f;
}
