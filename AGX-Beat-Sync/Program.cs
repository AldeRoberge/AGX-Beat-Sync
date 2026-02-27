using AGX_Beat_Sync;
using AGX_Beat_Sync.Services;

// Register .agxbs so double-clicking opens this app (idempotent; no admin required)
FileAssociation.Register();

// Allow opening .agxbs from command line (e.g. when double-clicking a file or "Open with")
string? projectPath = null;
string[] cmdArgs = Environment.GetCommandLineArgs();
for (int i = 1; i < cmdArgs.Length; i++)
{
    string arg = cmdArgs[i].Trim().Trim('"');
    if (arg.EndsWith(".agxbs", StringComparison.OrdinalIgnoreCase) && File.Exists(arg))
    {
        projectPath = Path.GetFullPath(arg);
        break;
    }
}
if (projectPath != null)
    BeatSyncGame.StartupProjectPath = projectPath;

using var game = new BeatSyncGame();
game.Run();