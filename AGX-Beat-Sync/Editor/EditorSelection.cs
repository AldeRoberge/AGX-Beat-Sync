using AGX_Beat_Sync.Core;

namespace AGX_Beat_Sync.Editor;

/// <summary>
/// Currently selected objects for the inspector and editing commands.
/// Unified: selection is event tracks (and optionally a specific event time on the timeline).
/// </summary>
public class EditorSelection
{
    public AutomationKeyframe? SelectedKeyframe { get; set; }
    public AutomationTrack? SelectedAutomationTrack { get; set; }
    /// <summary>Selected event track. Inspector shows this track's properties; timeline highlights its row and events.</summary>
    public IEventTrack? SelectedEventTrack { get; set; }
    /// <summary>Selected event time (seconds) on the timeline. Null if no specific event is selected.</summary>
    public double? SelectedEventTime { get; set; }

    public void Clear()
    {
        SelectedKeyframe = null;
        SelectedAutomationTrack = null;
        SelectedEventTrack = null;
        SelectedEventTime = null;
    }

    public bool HasSelection => SelectedKeyframe != null || SelectedEventTrack != null;
}
