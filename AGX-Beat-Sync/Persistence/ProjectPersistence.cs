using System.Text.Json;
using AGX_Beat_Sync.Core;

namespace AGX_Beat_Sync.Persistence;

/// <summary>Save and load project + transport state to a JSON file in AppData.</summary>
public static class ProjectPersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string GetStatePath()
    {
        return Path.Combine(GetAppDataDir(), "state.json");
    }

    /// <summary>Save current project and transport to the session state file in AppData.</summary>
    public static void Save(Project project, Transport transport)
    {
        SaveToFile(project, transport, GetStatePath());
    }

    /// <summary>Save current project and transport to a specific file path (e.g. .agxbs).</summary>
    public static void SaveToFile(Project project, Transport transport, string filePath)
    {
        try
        {
            var state = new SavedSessionState
            {
                AudioFilePath = project.AudioFilePath ?? string.Empty,
                BPM = project.BPM,
                TimeSignatureNumerator = project.TimeSignatureNumerator,
                TimeSignatureDenominator = project.TimeSignatureDenominator,
                BeatOffsetSeconds = project.BeatOffsetSeconds,
                CurrentTime = transport.CurrentTime,
                AutomationTracks = project.AutomationTracks,
                EventTracks = project.EventTracks
            };
            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(filePath, json);
        }
        catch
        {
            // Caller may show error; rethrow so they know it failed
            throw;
        }
    }

    /// <summary>Try to load saved state from the default session file. Returns true if state was loaded.</summary>
    public static bool TryLoad(out SavedSessionState? state)
    {
        state = null;
        try
        {
            var path = GetStatePath();
            if (!File.Exists(path)) return false;
            return TryLoadFromFile(path, out state);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Try to load saved state from a specific file (e.g. .agxbs). Returns true if state was loaded.</summary>
    public static bool TryLoadFromFile(string filePath, out SavedSessionState? state)
    {
        state = null;
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;
            var json = File.ReadAllText(filePath);
            state = JsonSerializer.Deserialize<SavedSessionState>(json, JsonOptions);
            return state != null;
        }
        catch
        {
            return false;
        }
    }

    private static string GetAppDataDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "AGX", "Beat-Sync");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string GetRecentPath() => Path.Combine(GetAppDataDir(), "recent.json");

    private const int MaxRecentCount = 10;

    /// <summary>Returns the list of recent project file paths (newest first).</summary>
    public static IReadOnlyList<string> GetRecentProjectPaths()
    {
        try
        {
            var path = GetRecentPath();
            if (!File.Exists(path)) return Array.Empty<string>();
            var json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list?.Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p)).Take(MaxRecentCount).ToList() ?? new List<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Adds a project path to the recent list (moved to front, max 10).</summary>
    public static void AddRecentProjectPath(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath)) return;
        try
        {
            var path = GetRecentPath();
            var list = new List<string> { projectPath };
            if (File.Exists(path))
            {
                var existing = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path));
                if (existing != null)
                    list.AddRange(existing.Where(p => !string.IsNullOrWhiteSpace(p) && string.Equals(p, projectPath, StringComparison.OrdinalIgnoreCase) == false));
            }
            var trimmed = list.Take(MaxRecentCount).ToList();
            File.WriteAllText(path, JsonSerializer.Serialize(trimmed, JsonOptions));
        }
        catch
        {
            // Ignore
        }
    }

    /// <summary>Apply a loaded state to project and transport.</summary>
    public static void ApplyState(SavedSessionState saved, Project project, Transport transport)
    {
        project.AudioFilePath = saved.AudioFilePath ?? string.Empty;
        project.BPM = saved.BPM;
        project.TimeSignatureNumerator = saved.TimeSignatureNumerator;
        project.TimeSignatureDenominator = saved.TimeSignatureDenominator;
        project.BeatOffsetSeconds = saved.BeatOffsetSeconds;
        project.AutomationTracks.Clear();
        foreach (var t in saved.AutomationTracks ?? [])
            project.AutomationTracks.Add(t);
        project.EventTracks.Clear();
        foreach (var t in saved.EventTracks ?? [])
            project.EventTracks.Add(t);

        transport.BPM = saved.BPM;
        transport.BeatOffsetSeconds = saved.BeatOffsetSeconds;
        transport.CurrentTime = Math.Max(0, saved.CurrentTime);
    }
}
