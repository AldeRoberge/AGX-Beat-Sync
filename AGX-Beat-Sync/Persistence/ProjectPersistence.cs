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
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "AGX-Beat-Sync");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "state.json");
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
                NoteTracks = project.NoteTracks,
                AutomationTracks = project.AutomationTracks
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

    /// <summary>Try to load saved state. Returns true if state was loaded and applied.</summary>
    public static bool TryLoad(out SavedSessionState? state)
    {
        state = null;
        try
        {
            var path = GetStatePath();
            if (!File.Exists(path)) return false;
            var json = File.ReadAllText(path);
            state = JsonSerializer.Deserialize<SavedSessionState>(json, JsonOptions);
            return state != null;
        }
        catch
        {
            return false;
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
        project.NoteTracks.Clear();
        foreach (var t in saved.NoteTracks ?? [])
            project.NoteTracks.Add(t);
        project.AutomationTracks.Clear();
        foreach (var t in saved.AutomationTracks ?? [])
            project.AutomationTracks.Add(t);

        transport.BPM = saved.BPM;
        transport.BeatOffsetSeconds = saved.BeatOffsetSeconds;
        transport.CurrentTime = Math.Max(0, saved.CurrentTime);
    }
}
