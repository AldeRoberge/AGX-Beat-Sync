using AGX_Beat_Sync.Audio;

namespace AGX_Beat_Sync.Services;

/// <summary>
/// Runs waveform build and BPM detection on a background task and reports progress.
/// Caller applies the result on the main thread (audio load, cache swap, BPM).
/// </summary>
public sealed class AudioLoadCoordinator
{
    private readonly object _progressLock = new();
    private double _progress;
    private Task<(string path, WaveformCache cache, double? bpm)>? _task;

    /// <summary>True while a load is in progress.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Progress in [0, 1]. Thread-safe.</summary>
    public double Progress
    {
        get { lock (_progressLock) return _progress; }
        private set { lock (_progressLock) _progress = value; }
    }

    /// <summary>
    /// Starts loading waveform for the given path and, optionally, runs BPM detection.
    /// No-op if already loading.
    /// </summary>
    public void Start(string audioFilePath, bool detectBpm)
    {
        if (IsLoading) return;
        IsLoading = true;
        Progress = 0;
        IProgress<double>? bpmProgress = detectBpm
            ? new Progress<double>(p => Progress = 0.4 + 0.6 * p)
            : null;
        _task = Task.Run(() => RunLoad(audioFilePath, detectBpm, bpmProgress));
    }

    /// <summary>If the current task has completed, returns true and sets result. Clears loading state.</summary>
    public bool TryComplete(out string path, out WaveformCache cache, out double? bpm)
    {
        path = null!;
        cache = null!;
        bpm = null;
        if (!IsLoading || _task == null) return false;
        if (!_task.IsCompleted) return false;

        try
        {
            (path, cache, bpm) = _task.GetAwaiter().GetResult();
            return true;
        }
        finally
        {
            _task = null;
            IsLoading = false;
        }
    }

    /// <summary>Call when load failed or was cancelled; resets state.</summary>
    public void Reset()
    {
        _task = null;
        IsLoading = false;
    }

    private static (string path, WaveformCache cache, double? bpm) RunLoad(
        string audioFilePath,
        bool detectBpm,
        IProgress<double>? bpmProgress)
    {
        var cache = new WaveformCache();
        cache.Load(audioFilePath);

        double? bpm = null;
        if (detectBpm)
        {
            bpmProgress?.Report(0); // waveform done; BPM phase will report 0.05..1.0
            var candidates = BPMDetector.Detect(audioFilePath, bpmProgress);
            bpm = candidates.Count > 0 ? Math.Clamp(candidates[0].BPM, 20, 999) : null;
        }

        // When we skip BPM detection, consider load "complete" immediately.
        if (!detectBpm)
            bpmProgress?.Report(1.0);

        return (audioFilePath, cache, bpm);
    }
}
