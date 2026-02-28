using System.Collections.Generic;
using AGX_Beat_Sync.Core;

namespace AGX_Beat_Sync.Editor;

/// <summary>
/// Currently selected objects for the inspector and editing commands.
/// Unified: selection is event tracks (and optionally a specific event time on the timeline).
/// Supports multi-selection of tracks (Ctrl/Shift in track list) and notes (rectangle select); primary selection is the first for inspector.
/// </summary>
public class EditorSelection
{
    public AutomationKeyframe? SelectedKeyframe { get; set; }
    public AutomationTrack? SelectedAutomationTrack { get; set; }
    private readonly List<IEventTrack> _selectedEventTracks = new();
    private double? _selectedEventTime;

    /// <summary>All selected event tracks. Primary (for inspector) is the first. Empty when none selected.</summary>
    public IReadOnlyList<IEventTrack> SelectedEventTracks => _selectedEventTracks;

    /// <summary>Primary selected event track (first in list). Inspector shows this track's properties; timeline highlights its row and events.</summary>
    public IEventTrack? SelectedEventTrack
    {
        get => _selectedEventTracks.Count > 0 ? _selectedEventTracks[0] : null;
        set
        {
            _selectedEventTracks.Clear();
            if (value != null)
                _selectedEventTracks.Add(value);
            SyncNotesFromPrimary();
        }
    }

    private void SyncNotesFromPrimary()
    {
        var primary = SelectedEventTrack;
        if (primary == null)
            SelectedNotes.Clear();
        else if (_selectedEventTime.HasValue)
        {
            SelectedNotes.Clear();
            SelectedNotes.Add((primary, _selectedEventTime.Value));
        }
    }

    /// <summary>True if the given track is in the current track selection.</summary>
    public bool IsTrackSelected(IEventTrack track)
    {
        return _selectedEventTracks.Contains(track);
    }

    /// <summary>Replace track selection with the given set. Primary is the first. Pass empty to clear.</summary>
    public void SetSelectedTracks(IEnumerable<IEventTrack> tracks)
    {
        _selectedEventTracks.Clear();
        foreach (var t in tracks)
            _selectedEventTracks.Add(t);
        SyncNotesFromPrimary();
    }

    /// <summary>Toggle one track in selection. If not selected, add; if selected, remove. Primary becomes first remaining.</summary>
    public void ToggleTrackSelection(IEventTrack track)
    {
        int idx = _selectedEventTracks.IndexOf(track);
        if (idx >= 0)
            _selectedEventTracks.RemoveAt(idx);
        else
            _selectedEventTracks.Add(track);
        SyncNotesFromPrimary();
    }

    /// <summary>Select a range of tracks by index (inclusive). Clears current selection. Primary is tracks[from].</summary>
    public void SelectTrackRange(int fromIndex, int toIndex, IList<IEventTrack> tracks)
    {
        int lo = Math.Min(fromIndex, toIndex);
        int hi = Math.Max(fromIndex, toIndex);
        _selectedEventTracks.Clear();
        for (int i = lo; i <= hi && i < tracks.Count; i++)
            _selectedEventTracks.Add(tracks[i]);
        SyncNotesFromPrimary();
    }

    /// <summary>Remove tracks from selection (e.g. after delete). Updates primary from first remaining.</summary>
    public void RemoveTracksFromSelection(IEnumerable<IEventTrack> toRemove)
    {
        var set = new HashSet<IEventTrack>(toRemove);
        for (int i = _selectedEventTracks.Count - 1; i >= 0; i--)
        {
            if (set.Contains(_selectedEventTracks[i]))
                _selectedEventTracks.RemoveAt(i);
        }
        SyncNotesFromPrimary();
    }

    /// <summary>Selected event time (seconds) on the timeline. Null if no specific event is selected.</summary>
    public double? SelectedEventTime
    {
        get => _selectedEventTime;
        set
        {
            _selectedEventTime = value;
            if (value == null)
                SelectedNotes.Clear();
            else if (SelectedEventTrack != null)
            {
                SelectedNotes.Clear();
                SelectedNotes.Add((SelectedEventTrack, value.Value));
            }
        }
    }

    /// <summary>All selected notes (track + event time). Primary selection is the first; used for multi-select and rectangle select.</summary>
    public List<(IEventTrack Track, double EventTime)> SelectedNotes { get; } = new();

    /// <summary>True if the given note is in the current selection.</summary>
    public bool IsNoteSelected(IEventTrack track, double eventTime)
    {
        const double eps = 0.0001;
        foreach (var (t, et) in SelectedNotes)
        {
            if (t == track && Math.Abs(et - eventTime) < eps)
                return true;
        }
        return false;
    }

    /// <summary>Set selection to a single note (and clear multi-selection).</summary>
    public void SetSingleNote(IEventTrack track, double eventTime)
    {
        SelectedNotes.Clear();
        SelectedNotes.Add((track, eventTime));
        _selectedEventTracks.Clear();
        _selectedEventTracks.Add(track);
        _selectedEventTime = eventTime;
    }

    /// <summary>Set selection to multiple notes; primary is the first. Pass empty to clear note selection.</summary>
    public void SetSelectedNotes(IEnumerable<(IEventTrack Track, double EventTime)> notes)
    {
        SelectedNotes.Clear();
        foreach (var n in notes)
            SelectedNotes.Add(n);
        _selectedEventTracks.Clear();
        if (SelectedNotes.Count > 0)
        {
            _selectedEventTracks.Add(SelectedNotes[0].Track);
            _selectedEventTime = SelectedNotes[0].EventTime;
        }
        else
            _selectedEventTime = null;
    }

    /// <summary>Remove one note from selection (e.g. after delete). Updates primary from first remaining.</summary>
    public void RemoveNoteFromSelection(IEventTrack track, double eventTime)
    {
        const double eps = 0.0001;
        for (int i = SelectedNotes.Count - 1; i >= 0; i--)
        {
            if (SelectedNotes[i].Track == track && Math.Abs(SelectedNotes[i].EventTime - eventTime) < eps)
            {
                SelectedNotes.RemoveAt(i);
                break;
            }
        }
        _selectedEventTracks.Clear();
        if (SelectedNotes.Count > 0)
        {
            _selectedEventTracks.Add(SelectedNotes[0].Track);
            _selectedEventTime = SelectedNotes[0].EventTime;
        }
        else
            _selectedEventTime = null;
    }

    public void Clear()
    {
        SelectedKeyframe = null;
        SelectedAutomationTrack = null;
        _selectedEventTracks.Clear();
        _selectedEventTime = null;
        SelectedNotes.Clear();
    }

    public bool HasSelection => SelectedKeyframe != null || _selectedEventTracks.Count > 0;
}
