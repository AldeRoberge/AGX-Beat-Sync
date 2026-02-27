namespace AGX_Beat_Sync.Core;

/// <summary>
/// Central transport: playhead, play state, BPM, and time conversions.
/// All tracks evaluate based on CurrentTime.
/// </summary>
public class Transport
{
    public double CurrentTime { get; set; }
    public bool IsPlaying { get; set; }
    public double BPM { get; set; } = 120.0;
    /// <summary>Time in seconds where beat 0 of the grid sits (set from project In point). Used for grid drawing and snap-to-beat.</summary>
    public double BeatOffsetSeconds { get; set; }

    public double SecondsPerBeat => 60.0 / BPM;

    public void Play() => IsPlaying = true;
    public void Pause() => IsPlaying = false;
    public void Stop()
    {
        IsPlaying = false;
        CurrentTime = 0;
    }

    public void Seek(double time) => CurrentTime = Math.Max(0, time);

    /// <summary>Convert beat index to seconds (relative to beat 0 at BeatOffsetSeconds).</summary>
    public double BeatToSeconds(double beat) => BeatOffsetSeconds + beat * SecondsPerBeat;

    /// <summary>Convert seconds to beat index.</summary>
    public double SecondsToBeat(double seconds) => (seconds - BeatOffsetSeconds) / SecondsPerBeat;

    /// <summary>Snap a time (seconds) to the beat grid. Subdivisions: 1=whole beat, 2=half, 4=quarter, etc.</summary>
    public double SnapToBeat(double time, int subdivisionsPerBeat = 1)
    {
        if (subdivisionsPerBeat < 1) subdivisionsPerBeat = 1;
        double beat = SecondsToBeat(time);
        double snappedBeat = Math.Round(beat * subdivisionsPerBeat) / subdivisionsPerBeat;
        return BeatToSeconds(snappedBeat);
    }
}
