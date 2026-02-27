using AGX_Beat_Sync;

// Allow opening .agxbs from command line (e.g. when double-clicking a file or "Open with")
string? projectPath = null;
string[] args = Environment.GetCommandLineArgs();
for (int i = 1; i < args.Length; i++)
{
    string arg = args[i].Trim().Trim('"');
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