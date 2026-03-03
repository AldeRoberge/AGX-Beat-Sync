using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AGX_Beat_Sync.Services;

/// <summary>
/// Downloads audio from a URL (YouTube, SoundCloud, etc.) as MP3 using yt-dlp or youtube-dl.
/// Requires yt-dlp (or youtube-dl) to be installed and available in PATH.
/// </summary>
public static class UrlToMp3Service
{
    private static readonly string[] ExecutableNames = { "yt-dlp.exe", "yt-dlp", "youtube-dl.exe", "youtube-dl" };

    /// <summary>
    /// Tries to find yt-dlp or youtube-dl in PATH. Returns the executable path or null.
    /// </summary>
    public static string? FindDownloader()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var dirs = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var exe in ExecutableNames)
        {
            foreach (var dir in dirs)
            {
                var full = Path.Combine(dir.Trim(), exe);
                if (File.Exists(full))
                    return full;
            }
        }
        return null;
    }

    /// <summary>
    /// Downloads audio from the given URL as MP3. Supports YouTube and SoundCloud URLs.
    /// Returns the path to the downloaded file, or null on failure.
    /// </summary>
    /// <param name="url">YouTube or SoundCloud (or other supported) URL.</param>
    /// <param name="outputDirectory">Directory to save the MP3; created if needed. If null, uses My Music\AGX-Beat-Sync.</param>
    /// <param name="progress">Optional progress reporter (0..1).</param>
    /// <param name="cancellationToken">Optional cancellation.</param>
    public static string? Download(
        string url,
        string? outputDirectory = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var exe = FindDownloader();
        if (exe == null)
        {
            EngineLogs.Logger.LogWarning("UrlToMp3: yt-dlp or youtube-dl not found in PATH. Install from https://github.com/yt-dlp/yt-dlp");
            return null;
        }

        outputDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            "AGX-Beat-Sync");
        try
        {
            Directory.CreateDirectory(outputDirectory);
        }
        catch (Exception ex)
        {
            EngineLogs.Logger.LogWarning(ex, "UrlToMp3: Could not create output directory {Dir}", outputDirectory);
            return null;
        }

        // Output template: title with sanitized filename, always .mp3
        var outputTemplate = Path.Combine(outputDirectory, "%(title).200s.%(ext)s");

        var args = new List<string>
        {
            "--no-playlist",
            "-x",
            "--audio-format", "mp3",
            "--audio-quality", "0",
            "-o", outputTemplate,
            "--no-warnings",
            "--newline",
            url
        };

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = string.Join(" ", args.Select(a => ProcessArgument(a))),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = outputDirectory
            }
        };

        var outputPath = new List<string>();
        var progressRegex = new Regex(@"\[download\]\s+(\d+\.?\d*)%", RegexOptions.Compiled);
        var lastProgress = 0.0;

        void OnOutput(string? line)
        {
            if (string.IsNullOrEmpty(line)) return;
            const string destPrefix = "Destination: ";
            int idx = line.IndexOf(destPrefix, StringComparison.Ordinal);
            if (idx >= 0 && (line.StartsWith("[download] ", StringComparison.Ordinal) || line.StartsWith("[ExtractAudio] ", StringComparison.Ordinal)))
                outputPath.Add(line.Substring(idx + destPrefix.Length).Trim());
            var m = progressRegex.Match(line);
            if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pct))
            {
                lastProgress = Math.Min(100, pct) / 100.0;
                progress?.Report(0.2 + 0.7 * lastProgress);
            }
        }

        process.OutputDataReceived += (_, e) => OnOutput(e.Data);
        process.ErrorDataReceived += (_, e) => OnOutput(e.Data);

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            while (!process.HasExited && !cancellationToken.IsCancellationRequested)
            {
                if (process.WaitForExit(200))
                    break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            process.WaitForExit(TimeSpan.FromSeconds(2));

            progress?.Report(1.0);

            if (process.ExitCode != 0)
            {
                EngineLogs.Logger.LogWarning("UrlToMp3: Process exited with code {Code}", process.ExitCode);
                return null;
            }

            // Find the downloaded file: last path we saw, or first .mp3 in output dir
            string? path = null;
            foreach (var p in outputPath)
            {
                if (p.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) && File.Exists(p))
                {
                    path = p;
                    break;
                }
            }
            if (path == null)
            {
                var mp3s = Directory.GetFiles(outputDirectory, "*.mp3");
                path = mp3s
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            if (path != null)
                EngineLogs.Logger.LogInformation("UrlToMp3: Downloaded {File}", Path.GetFileName(path));
            return path;
        }
        catch (Exception ex)
        {
            EngineLogs.Logger.LogWarning(ex, "UrlToMp3: Download failed");
            return null;
        }
    }

    private static string ProcessArgument(string arg)
    {
        if (arg.Contains(' ') || arg.Contains('"'))
            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        return arg;
    }
}
