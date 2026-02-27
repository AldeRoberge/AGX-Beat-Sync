using AGX_Beat_Sync.Core;

namespace AGX_Beat_Sync.Editor;

/// <summary>
/// Currently selected objects for the inspector and editing commands.
/// </summary>
public class EditorSelection
{
    public NoteEvent? SelectedNote { get; set; }
    public NoteTrack? SelectedNoteTrack { get; set; }
    /// <summary>When set, all selected notes (multi-select). Primary for inspector is SelectedNote.</summary>
    public List<NoteEvent> SelectedNotes { get; set; } = new();
    public AutomationKeyframe? SelectedKeyframe { get; set; }
    public AutomationTrack? SelectedAutomationTrack { get; set; }

    public void Clear()
    {
        SelectedNote = null;
        SelectedNoteTrack = null;
        SelectedNotes.Clear();
        SelectedKeyframe = null;
        SelectedAutomationTrack = null;
    }

    public bool HasSelection => SelectedNote != null || SelectedNotes.Count > 0 || SelectedKeyframe != null;

    /// <summary>True if the given note is in the current selection.</summary>
    public bool IsSelected(NoteEvent note)
    {
        if (note == null) return false;
        if (SelectedNotes.Count > 0) return SelectedNotes.Contains(note);
        return SelectedNote == note;
    }

    /// <summary>Set selection to a single note.</summary>
    public void SetSingle(NoteEvent? note, NoteTrack? track)
    {
        SelectedNote = note;
        SelectedNoteTrack = track;
        SelectedNotes.Clear();
        if (note != null) SelectedNotes.Add(note);
    }

    /// <summary>Set selection to all notes in the track.</summary>
    public void SetAllNotes(NoteTrack track)
    {
        SelectedNoteTrack = track;
        SelectedNotes = new List<NoteEvent>(track.Notes);
        SelectedNote = SelectedNotes.Count > 0 ? SelectedNotes[0] : null;
    }
}
