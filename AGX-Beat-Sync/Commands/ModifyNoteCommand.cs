using AGX_Beat_Sync.Core;

namespace AGX_Beat_Sync.Commands;

public class ModifyNoteCommand : ICommand
{
    private readonly NoteTrack _track;
    private readonly NoteEvent _note;
    private readonly double _oldTime;
    private readonly int _oldLane;
    private readonly double _newTime;
    private readonly int _newLane;

    public ModifyNoteCommand(NoteTrack track, NoteEvent note, double oldTime, int oldLane, double newTime, int newLane)
    {
        _track = track;
        _note = note;
        _oldTime = oldTime;
        _oldLane = oldLane;
        _newTime = newTime;
        _newLane = newLane;
    }

    public void Execute()
    {
        _note.Time = _newTime;
        _note.Lane = _newLane;
        _track.Notes.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    public void Undo()
    {
        _note.Time = _oldTime;
        _note.Lane = _oldLane;
        _track.Notes.Sort((a, b) => a.Time.CompareTo(b.Time));
    }
}
