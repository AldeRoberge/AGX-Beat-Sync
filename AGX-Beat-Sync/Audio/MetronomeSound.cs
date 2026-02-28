using NAudio.Wave;

namespace AGX_Beat_Sync.Audio;

/// <summary>
/// Plays short tick (high) and tock (low) tones for the metronome, Ableton-style.
/// Tock on beat 1, tick on beats 2–4. Uses generated sine bursts.
/// </summary>
public static class MetronomeSound
{
    private const int SampleRate = 44100;
    private const double DurationSeconds = 0.04;
    private const float TickFrequencyHz = 1100f;
    private const float TockFrequencyHz = 680f;
    private const float Amplitude = 0.35f;

    private static WaveFormat? _format;
    private static byte[]? _tickBuffer;
    private static byte[]? _tockBuffer;

    private static WaveFormat Format => _format ??= new WaveFormat(SampleRate, 16, 1);

    private static byte[] TickBuffer
    {
        get
        {
            if (_tickBuffer == null)
                _tickBuffer = BuildToneBuffer(TickFrequencyHz);
            return _tickBuffer;
        }
    }

    private static byte[] TockBuffer
    {
        get
        {
            if (_tockBuffer == null)
                _tockBuffer = BuildToneBuffer(TockFrequencyHz);
            return _tockBuffer;
        }
    }

    /// <summary>Play a high "tick" (beats 2, 3, 4).</summary>
    public static void PlayTick(float volume = 1f) => Play(TickBuffer, volume);

    /// <summary>Play a low "tock" (beat 1 / downbeat).</summary>
    public static void PlayTock(float volume = 1f) => Play(TockBuffer, volume);

    private static void Play(byte[] buffer, float volume)
    {
        var ms = new MemoryStream(buffer);
        var stream = new RawSourceWaveStream(ms, Format);
        var waveOut = new WaveOutEvent();
        waveOut.Volume = Math.Clamp(volume, 0f, 1f);
        waveOut.Init(stream);
        waveOut.PlaybackStopped += (_, _) =>
        {
            waveOut.Dispose();
            stream.Dispose();
            ms.Dispose();
        };
        waveOut.Play();
    }

    private static byte[] BuildToneBuffer(float frequencyHz)
    {
        int sampleCount = (int)(SampleRate * DurationSeconds);
        var samples = new short[sampleCount];
        double omega = 2.0 * Math.PI * frequencyHz / SampleRate;
        for (int i = 0; i < sampleCount; i++)
        {
            // Sine with quick decay envelope (so it's a soft click, not a sustained tone)
            double t = (double)i / sampleCount;
            double envelope = 1.0 - t * t; // parabolic decay
            double sample = Amplitude * Math.Sin(omega * i) * envelope;
            samples[i] = (short)Math.Clamp((int)(sample * short.MaxValue), short.MinValue, short.MaxValue);
        }
        var bytes = new byte[sampleCount * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
