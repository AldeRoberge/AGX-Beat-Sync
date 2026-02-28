using AGX_Beat_Sync.Core;

namespace AGX_Beat_Sync.Commands;

public class SetInOutCommand : ICommand
{
    private readonly Project _project;
    private readonly double? _oldIn;
    private readonly double? _oldOut;
    private readonly double? _newIn;
    private readonly double? _newOut;

    public SetInOutCommand(Project project, double? newIn, double? newOut)
    {
        _project = project;
        _oldIn = project.InTime;
        _oldOut = project.OutTime;
        _newIn = newIn;
        _newOut = newOut;
    }

    public void Execute()
    {
        _project.InTime = _newIn;
        _project.OutTime = _newOut;
    }

    public void Undo()
    {
        _project.InTime = _oldIn;
        _project.OutTime = _oldOut;
    }
}
