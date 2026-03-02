namespace AGX_Beat_Sync.Persistence;

/// <summary>Metadata section for .agxbs files (version, title, author, timestamps).</summary>
public class ProjectMetadata
{
    /// <summary>AGXBS file format version for compatibility. Current = 1.</summary>
    public int ProjectFormatVersion { get; set; } = 1;

    /// <summary>Application version that wrote this file (e.g. "1.0.0").</summary>
    public string? AppVersion { get; set; }

    /// <summary>User-defined project title.</summary>
    public string? Title { get; set; }

    /// <summary>User-defined project description.</summary>
    public string? Description { get; set; }

    /// <summary>User-defined author name.</summary>
    public string? Author { get; set; }

    /// <summary>When the project was first created (ISO 8601 UTC).</summary>
    public string? TimeCreatedUtc { get; set; }

    /// <summary>Total time spent editing this project in seconds (accumulated across sessions).</summary>
    public double TotalEditingTimeSeconds { get; set; }
}
