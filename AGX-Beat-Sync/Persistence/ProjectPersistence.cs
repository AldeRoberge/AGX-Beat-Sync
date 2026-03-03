using System.Reflection;
using System.Text.Json;
using AGX_Beat_Sync.Audio;
using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Editor;

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

    /// <summary>Save current project, transport, optional timeline view, layout, camera, and window size.
    /// If currentProjectPath is set (e.g. to an .agxbs file), saves state.json in that project folder; otherwise saves to AppData.</summary>
    public static void Save(Project project, Transport transport, TimelineViewState? timelineView = null, int? gameViewHeightPx = null, int? gameViewWidthPx = null, int? inspectorWidthPx = null,
        float? cameraTargetX = null, float? cameraTargetY = null, float? cameraTargetZ = null, float? cameraOrbitYaw = null, float? cameraOrbitPitch = null, float? cameraOrbitDistance = null,
        int? windowWidthPx = null, int? windowHeightPx = null, string? currentProjectPath = null)
    {
        string path = !string.IsNullOrWhiteSpace(currentProjectPath) && currentProjectPath.EndsWith(".agxbs", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(Path.GetDirectoryName(currentProjectPath) ?? "", "state.json")
            : GetStatePath();
        SaveToFile(project, transport, path, timelineView, gameViewHeightPx, gameViewWidthPx, inspectorWidthPx, cameraTargetX, cameraTargetY, cameraTargetZ, cameraOrbitYaw, cameraOrbitPitch, cameraOrbitDistance, windowWidthPx, windowHeightPx);
    }

    /// <summary>Save current project, transport, optional timeline view, layout, camera, and window size to a specific file path (e.g. .agxbs).
    /// For .agxbs paths: creates a project folder when needed, copies imported music and .agxwf into it, stores relative audio path, and writes/preserves metadata (version, title, description, author, time created, total editing time).
    /// <param name="sessionEditingTimeSeconds">Time spent editing in this session (seconds). For .agxbs this is added to TotalEditingTimeSeconds; pass 0 to skip.</param>
    /// Returns the actual path where the file was written.</summary>
    public static string SaveToFile(Project project, Transport transport, string filePath, TimelineViewState? timelineView = null, int? gameViewHeightPx = null, int? gameViewWidthPx = null, int? inspectorWidthPx = null,
        float? cameraTargetX = null, float? cameraTargetY = null, float? cameraTargetZ = null, float? cameraOrbitYaw = null, float? cameraOrbitPitch = null, float? cameraOrbitDistance = null,
        int? windowWidthPx = null, int? windowHeightPx = null, double sessionEditingTimeSeconds = 0)
    {
        bool isAgxbs = filePath.EndsWith(".agxbs", StringComparison.OrdinalIgnoreCase);
        string projectDir;
        string actualFilePath;
        string audioPathInState = project.AudioFilePath ?? string.Empty;

        if (isAgxbs)
        {
            string? parentDir = Path.GetDirectoryName(filePath);
            string baseName = Path.GetFileNameWithoutExtension(filePath);
            string fileName = Path.GetFileName(filePath);
            if (!string.IsNullOrEmpty(parentDir) && string.Equals(Path.GetFileName(parentDir), baseName, StringComparison.OrdinalIgnoreCase))
            {
                projectDir = parentDir;
                actualFilePath = filePath;
            }
            else
            {
                projectDir = Path.Combine(parentDir ?? "", baseName);
                Directory.CreateDirectory(projectDir);
                actualFilePath = Path.Combine(projectDir, fileName);
            }

            if (!string.IsNullOrWhiteSpace(project.AudioFilePath) && File.Exists(project.AudioFilePath))
            {
                string audioFileName = Path.GetFileName(project.AudioFilePath);
                string destAudio = Path.Combine(projectDir, audioFileName);
                try
                {
                    File.Copy(project.AudioFilePath, destAudio, overwrite: true);
                    string agxwfSource = WaveformCache.AgxwfPathForAudio(project.AudioFilePath);
                    if (File.Exists(agxwfSource))
                    {
                        string agxwfDest = Path.Combine(projectDir, Path.GetFileName(agxwfSource));
                        File.Copy(agxwfSource, agxwfDest, overwrite: true);
                    }
                }
                catch
                {
                    // Continue saving project; audio copy failure is non-fatal
                }
                audioPathInState = audioFileName;
            }
        }
        else
        {
            projectDir = Path.GetDirectoryName(filePath) ?? "";
            actualFilePath = filePath;
        }

        try
        {
            ProjectMetadata? metadata = null;

            if (isAgxbs)
            {
                if (File.Exists(actualFilePath))
                {
                    try
                    {
                        var existingJson = File.ReadAllText(actualFilePath);
                        var existing = JsonSerializer.Deserialize<SavedSessionState>(existingJson, JsonOptions);
                        if (existing?.Metadata != null)
                        {
                            metadata = new ProjectMetadata
                            {
                                ProjectFormatVersion = 1,
                                AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0",
                                Title = existing.Metadata.Title,
                                Description = existing.Metadata.Description,
                                Author = existing.Metadata.Author,
                                TimeCreatedUtc = existing.Metadata.TimeCreatedUtc,
                                TotalEditingTimeSeconds = (existing.Metadata.TotalEditingTimeSeconds >= 0 ? existing.Metadata.TotalEditingTimeSeconds : 0) + Math.Max(0, sessionEditingTimeSeconds)
                            };
                        }
                    }
                    catch
                    {
                        // Use defaults for new metadata
                    }
                }
                if (metadata == null)
                {
                    metadata = new ProjectMetadata
                    {
                        ProjectFormatVersion = 1,
                        AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0",
                        TimeCreatedUtc = DateTime.UtcNow.ToString("o"),
                        TotalEditingTimeSeconds = Math.Max(0, sessionEditingTimeSeconds)
                    };
                }
            }

            var state = new SavedSessionState
            {
                Metadata = metadata,
                AudioFilePath = audioPathInState,
                BPM = project.BPM,
                TimeSignatureNumerator = project.TimeSignatureNumerator,
                TimeSignatureDenominator = project.TimeSignatureDenominator,
                InTime = project.InTime,
                OutTime = project.OutTime,
                CurrentTime = transport.CurrentTime,
                AutomationTracks = project.AutomationTracks,
                EventTracks = project.EventTracks,
                ViewStartTime = timelineView?.ViewStartTime ?? 0,
                Zoom = timelineView != null ? Math.Clamp(timelineView.Zoom, 20f, 800f) : 80f,
                GridSubdivisionsPerBeat = timelineView != null ? Math.Clamp(timelineView.GridSubdivisionsPerBeat, TimelineViewState.MinGridSubdivisions, TimelineViewState.MaxGridSubdivisions) : 4,
                GameViewHeightPx = gameViewHeightPx ?? 120,
                GameViewWidthPx = gameViewWidthPx ?? 0,
                InspectorWidthPx = inspectorWidthPx ?? 0,
                WindowWidthPx = windowWidthPx ?? 0,
                WindowHeightPx = windowHeightPx ?? 0,
                CameraTargetX = cameraTargetX ?? 0,
                CameraTargetY = cameraTargetY ?? 0.5f,
                CameraTargetZ = cameraTargetZ ?? 0,
                CameraOrbitYaw = cameraOrbitYaw ?? -0.26f,
                CameraOrbitPitch = cameraOrbitPitch ?? -0.644f,
                CameraOrbitDistance = cameraOrbitDistance ?? 8f
            };
            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(actualFilePath, json);
            if (isAgxbs)
            {
                string stateJsonPath = Path.Combine(projectDir, "state.json");
                try { File.WriteAllText(stateJsonPath, json); } catch { /* non-fatal */ }
                string backupPath = Path.Combine(projectDir, Path.GetFileNameWithoutExtension(actualFilePath) + ".backup.0.agxbs");
                try { File.WriteAllText(backupPath, json); } catch { /* non-fatal */ }
            }
            return actualFilePath;
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

    /// <summary>Apply a loaded state to project, transport, and optional timeline view.</summary>
    public static void ApplyState(SavedSessionState saved, Project project, Transport transport, TimelineViewState? timelineView = null)
    {
        project.AudioFilePath = saved.AudioFilePath ?? string.Empty;
        project.BPM = saved.BPM;
        project.TimeSignatureNumerator = saved.TimeSignatureNumerator;
        project.TimeSignatureDenominator = saved.TimeSignatureDenominator;
        project.InTime = saved.InTime;
        project.OutTime = saved.OutTime;
        project.AutomationTracks.Clear();
        foreach (var t in saved.AutomationTracks ?? [])
            project.AutomationTracks.Add(t);
        project.EventTracks.Clear();
        foreach (var t in saved.EventTracks ?? [])
            project.EventTracks.Add(t);

        transport.BPM = saved.BPM;
        transport.BeatOffsetSeconds = saved.InTime ?? saved.BeatOffsetSeconds;
        transport.CurrentTime = Math.Max(0, saved.CurrentTime);
        if (project.InTime is { } inT && project.OutTime is { } outT && outT > inT)
            transport.CurrentTime = Math.Clamp(transport.CurrentTime, inT, outT);

        if (timelineView != null)
        {
            timelineView.ViewStartTime = Math.Max(0, saved.ViewStartTime);
            timelineView.Zoom = Math.Clamp(saved.Zoom, 20f, 800f);
            timelineView.GridSubdivisionsPerBeat = Math.Clamp(saved.GridSubdivisionsPerBeat, TimelineViewState.MinGridSubdivisions, TimelineViewState.MaxGridSubdivisions);
        }
    }
}
