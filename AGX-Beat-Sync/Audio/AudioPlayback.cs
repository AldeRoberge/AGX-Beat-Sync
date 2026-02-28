using NAudio.Wave;

namespace AGX_Beat_Sync.Audio;

/// <summary>
/// Loads and plays MP3/WAV via NAudio. Exposes current position for transport sync.
/// </summary>
public class AudioPlayback : IDisposable
{
    private AudioFileReader? _reader;
    private WaveOutEvent? _waveOut;
    private bool _disposed;
    private bool _suppressPlaybackStopped;

    public string? LoadedFilePath { get; private set; }
    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;
    public double DurationSeconds => _reader?.TotalTime.TotalSeconds ?? 0;

    /// <summary>Playback volume (0.0 to 1.0).</summary>
    public float Volume
    {
        get => _reader != null ? _reader.Volume : 1f;
        set
        {
            if (_reader != null)
                _reader.Volume = Math.Clamp(value, 0f, 1f);
        }
    }

    /// <summary>Current playback position in seconds.</summary>
    public double CurrentTimeSeconds
    {
        get
        {
            if (_reader == null) return 0;
            return _reader.CurrentTime.TotalSeconds;
        }
        set
        {
            if (_reader == null) return;
            var t = TimeSpan.FromSeconds(Math.Max(0, Math.Min(value, _reader.TotalTime.TotalSeconds)));
            _reader.CurrentTime = t;
        }
    }

    public bool Load(string filePath)
    {
        Unload();
        try
        {
            _reader = new AudioFileReader(filePath);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_reader);
            _waveOut.PlaybackStopped += (_, _) => { if (!_suppressPlaybackStopped) PlaybackStopped?.Invoke(); };
            LoadedFilePath = filePath;
            return true;
        }
        catch
        {
            Unload();
            return false;
        }
    }

    public void Unload()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;
        _reader?.Dispose();
        _reader = null;
        LoadedFilePath = null;
    }

    public void Play()
    {
        _waveOut?.Play();
    }

    public void Pause()
    {
        _waveOut?.Pause();
    }

    public void Stop()
    {
        if (_reader != null)
            _reader.CurrentTime = TimeSpan.Zero;
        _waveOut?.Stop();
    }

    /// <summary>Stop playback and flush output buffers without changing the read position. Use before Seek+Play when resuming after the user moved the playhead while paused, so no stale buffered audio is heard.</summary>
    public void StopOutputOnly()
    {
        _suppressPlaybackStopped = true;
        try
        {
            _waveOut?.Stop();
        }
        finally
        {
            _suppressPlaybackStopped = false;
        }
    }

    /// <summary>Seek to time in seconds.</summary>
    public void Seek(double seconds)
    {
        CurrentTimeSeconds = seconds;
    }

    public event Action? PlaybackStopped;

    public void Dispose()
    {
        if (_disposed) return;
        Unload();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
