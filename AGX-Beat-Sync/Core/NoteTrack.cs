namespace AGX_Beat_Sync.Core;

public class NoteTrack
{
    public string Name { get; set; } = "Note Track";
    public List<NoteEvent> Notes { get; set; } = new();
}
