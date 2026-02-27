namespace AGX_Beat_Sync.Commands;

public class CommandStack
{
    private readonly Stack<ICommand> _undo = new();
    private readonly Stack<ICommand> _redo = new();
    private const int MaxUndo = 100;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Execute(ICommand command)
    {
        command.Execute();
        _redo.Clear();
        _undo.Push(command);
        while (_undo.Count > MaxUndo)
        {
            // Drop oldest
            var list = _undo.ToList();
            _undo.Clear();
            for (int i = list.Count - 1; i >= 1; i--)
                _undo.Push(list[i]);
        }
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        var cmd = _undo.Pop();
        cmd.Undo();
        _redo.Push(cmd);
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        var cmd = _redo.Pop();
        cmd.Execute();
        _undo.Push(cmd);
    }
}
