using AGX_Beat_Sync.Core;

namespace AGX_Beat_Sync.Audio;

/// <summary>
/// Ableton-style metronome: tock on beat 1 (downbeat), tick on beats 2–4.
/// Call Update each frame when transport is playing and metronome is enabled.
/// Uses output latency compensation so the audible click aligns with the music.
/// </summary>
public class Metronome
{
    private long _lastTriggeredBeat = -1;
    private const int BeatsPerBar = 4;

    /// <summary>Master volume for metronome (0.0 to 1.0).</summary>
    public float Volume { get; set; } = 0.7f;

    /// <summary>
    /// Estimated output latency in seconds (audio buffer + playback delay).
    /// Subtracted from transport time so we trigger when the audible beat crosses, not the read position.
    /// Typical NAudio WaveOut is ~50–150 ms; default 0.1s. Increase if metronome sounds early.
    /// </summary>
    public double OutputLatencySeconds { get; set; } = 0.1;

    /// <summary>
    /// Update metronome: if we crossed a beat boundary, play tick or tock.
    /// Call only when transport is playing and metronome is enabled.
    /// </summary>
    public void Update(double prevTime, double currentTime, Transport transport)
    {
        if (transport.BPM <= 0) return;
        // Use latency-compensated time so we trigger when the *audible* playback crosses the beat
        double prev = prevTime - OutputLatencySeconds;
        double curr = currentTime - OutputLatencySeconds;
        double beatOffset = transport.BeatOffsetSeconds;
        double secondsPerBeat = transport.SecondsPerBeat;
        double prevBeat = (prev - beatOffset) / secondsPerBeat;
        double currentBeat = (curr - beatOffset) / secondsPerBeat;
        long prevBeatIndex = (long)Math.Floor(prevBeat);
        long currentBeatIndex = (long)Math.Floor(currentBeat);

        // Trigger only the current beat when we enter it (one click per frame; avoids burst on seek)
        if (currentBeatIndex >= 0 && currentBeatIndex > _lastTriggeredBeat)
        {
            _lastTriggeredBeat = currentBeatIndex;
            long beatInBar = ((currentBeatIndex % BeatsPerBar) + BeatsPerBar) % BeatsPerBar;
            if (beatInBar == 0)
                MetronomeSound.PlayTock(Volume);
            else
                MetronomeSound.PlayTick(Volume);
        }
    }

    /// <summary>Call when transport stops so the next play triggers from the current beat.</summary>
    public void Reset()
    {
        _lastTriggeredBeat = -1;
    }

    /// <summary>Call when user seeks so we don't re-trigger the beat at the seek position.</summary>
    public void SyncToTime(Transport transport)
    {
        double effectiveTime = transport.CurrentTime - OutputLatencySeconds;
        double beat = transport.SecondsToBeat(effectiveTime);
        _lastTriggeredBeat = (long)Math.Floor(beat);
    }
}
