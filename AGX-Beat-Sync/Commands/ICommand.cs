namespace AGX_Beat_Sync.Commands;

public interface ICommand
{
    void Execute();
    void Undo();
}
