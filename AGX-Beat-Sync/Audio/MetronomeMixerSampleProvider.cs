using NAudio.Wave;

namespace AGX_Beat_Sync.Audio;

/// <summary>
/// Wraps an audio source and mixes metronome tick/tock at exact sample positions so the click
/// is in the same buffer as the music and cannot drift. Use this when an audio file is loaded.
/// </summary>
public class MetronomeMixerSampleProvider : ISampleProvider
{
    private readonly AudioFileReader _reader;
    private readonly int _sampleRate;
    private readonly int _channels;
    private float[] _tickSamples = null!;
    private float[] _tockSamples = null!;
    private const int BeatsPerBar = 4;

    public WaveFormat WaveFormat => _reader.WaveFormat;

    /// <summary>BPM for beat grid. Tock on beat 0 (downbeat), tick on 1–3.</summary>
    public double BPM { get; set; } = 120;

    /// <summary>Time in seconds where beat 0 sits (e.g. project In point).</summary>
    public double BeatOffsetSeconds { get; set; }

    /// <summary>Metronome mix volume (0 = off).</summary>
    public float MetronomeVolume { get; set; }

    public MetronomeMixerSampleProvider(AudioFileReader reader)
    {
        _reader = reader;
        _sampleRate = reader.WaveFormat.SampleRate;
        _channels = reader.WaveFormat.Channels;
        _tickSamples = MetronomeSound.GetTickSamplesFloat(_sampleRate);
        _tockSamples = MetronomeSound.GetTockSamplesFloat(_sampleRate);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        double startTime = _reader.CurrentTime.TotalSeconds;
        int read = _reader.Read(buffer, offset, count);
        if (read <= 0 || MetronomeVolume <= 0 || BPM <= 0) return read;

        double secondsPerBeat = 60.0 / BPM;
        int frameCount = read / _channels;
        double endTime = startTime + frameCount / (double)_sampleRate;

        // Beat indices k such that beat time = BeatOffsetSeconds + k * secondsPerBeat is in [startTime, endTime]
        double startBeat = (startTime - BeatOffsetSeconds) / secondsPerBeat;
        double endBeat = (endTime - BeatOffsetSeconds) / secondsPerBeat;
        long kMin = (long)Math.Ceiling(startBeat);
        long kMax = (long)Math.Floor(endBeat);

        for (long k = kMin; k <= kMax; k++)
        {
            double beatTime = BeatOffsetSeconds + k * secondsPerBeat;
            int frameIdx = (int)Math.Round((beatTime - startTime) * _sampleRate);
            if (frameIdx < 0) continue;
            long beatInBar = ((k % BeatsPerBar) + BeatsPerBar) % BeatsPerBar;
            float[] tone = beatInBar == 0 ? _tockSamples : _tickSamples;
            for (int i = 0; i < tone.Length; i++)
            {
                int frame = frameIdx + i;
                if (frame >= frameCount) break;
                float mix = tone[i] * MetronomeVolume;
                for (int c = 0; c < _channels; c++)
                {
                    int bufIdx = offset + frame * _channels + c;
                    if (bufIdx < offset + read)
                        buffer[bufIdx] = Math.Clamp(buffer[bufIdx] + mix, -1f, 1f);
                }
            }
        }

        return read;
    }
}
