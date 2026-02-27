using AGX_Beat_Sync.Core;

namespace AGX_Beat_Sync.Commands;

public class AddNoteCommand : ICommand
{
    private readonly NoteTrack _track;
    private readonly NoteEvent _note;

    public AddNoteCommand(NoteTrack track, NoteEvent note)
    {
        _track = track;
        _note = note;
    }

    public void Execute()
    {
        _track.Notes.Add(_note);
        _track.Notes.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    public void Undo()
    {
        _track.Notes.Remove(_note);
    }
}
