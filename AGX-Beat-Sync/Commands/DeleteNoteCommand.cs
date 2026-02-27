using AGX_Beat_Sync.Core;

namespace AGX_Beat_Sync.Commands;

public class DeleteNoteCommand : ICommand
{
    private readonly NoteTrack _track;
    private readonly NoteEvent _note;
    private readonly int _index;

    public DeleteNoteCommand(NoteTrack track, NoteEvent note)
    {
        _track = track;
        _note = note;
        _index = track.Notes.IndexOf(note);
    }

    public void Execute()
    {
        _track.Notes.Remove(_note);
    }

    public void Undo()
    {
        if (_index >= 0 && _index <= _track.Notes.Count)
            _track.Notes.Insert(_index, _note);
        else
            _track.Notes.Add(_note);
        _track.Notes.Sort((a, b) => a.Time.CompareTo(b.Time));
    }
}
