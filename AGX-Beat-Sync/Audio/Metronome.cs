using AGX_Beat_Sync.Core;

namespace AGX_Beat_Sync.Audio;

/// <summary>
/// Ableton-style metronome: tock on beat 1 (downbeat), tick on beats 2–4.
/// Call Update each frame when transport is playing and metronome is enabled.
/// </summary>
public class Metronome
{
    private long _lastTriggeredBeat = -1;
    private const int BeatsPerBar = 4;

    /// <summary>Master volume for metronome (0.0 to 1.0).</summary>
    public float Volume { get; set; } = 0.7f;

    /// <summary>
    /// Update metronome: if we crossed a beat boundary, play tick or tock.
    /// Call only when transport is playing and metronome is enabled.
    /// </summary>
    public void Update(double prevTime, double currentTime, Transport transport)
    {
        if (transport.BPM <= 0) return;
        double beatOffset = transport.BeatOffsetSeconds;
        double secondsPerBeat = transport.SecondsPerBeat;
        double prevBeat = (prevTime - beatOffset) / secondsPerBeat;
        double currentBeat = (currentTime - beatOffset) / secondsPerBeat;
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
        double beat = transport.SecondsToBeat(transport.CurrentTime);
        _lastTriggeredBeat = (long)Math.Floor(beat);
    }
}
