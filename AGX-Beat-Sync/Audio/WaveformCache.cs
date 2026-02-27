using System.IO;
using System.Text;
using NAudio.Wave;

namespace AGX_Beat_Sync.Audio;

/// <summary>
/// Downsampled peak data for waveform display. One min/max per time bucket.
/// Can load/save .agxwf next to the audio file for fast reload.
/// </summary>
public class WaveformCache
{
    private const int MaxBuckets = 4096;
    private static readonly byte[] AgxwfMagic = Encoding.ASCII.GetBytes("AGXWF");
    private const ushort AgxwfVersion = 1;

    public double DurationSeconds { get; private set; }
    public int BucketCount { get; private set; }
    public float[] MinPeaks { get; private set; } = Array.Empty<float>();
    public float[] MaxPeaks { get; private set; } = Array.Empty<float>();

    public string? LoadedPath { get; private set; }

    public static string AgxwfPathForAudio(string audioFilePath)
    {
        return Path.ChangeExtension(audioFilePath, ".agxwf");
    }

    /// <summary>Try to load cached waveform from .agxwf next to the audio file.</summary>
    public bool TryLoadFromAgxwf(string audioFilePath)
    {
        string wfPath = AgxwfPathForAudio(audioFilePath);
        if (!File.Exists(wfPath)) return false;
        try
        {
            using var fs = new FileStream(wfPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);
            byte[] magic = br.ReadBytes(5);
            if (magic.Length != 5 || !magic.SequenceEqual(AgxwfMagic))
                return false;
            ushort version = br.ReadUInt16();
            if (version != AgxwfVersion) return false;
            double duration = br.ReadDouble();
            int bucketCount = br.ReadInt32();
            if (duration <= 0 || bucketCount <= 0 || bucketCount > MaxBuckets * 2)
                return false;
            var minPeaks = new float[bucketCount];
            var maxPeaks = new float[bucketCount];
            for (int i = 0; i < bucketCount; i++)
                minPeaks[i] = br.ReadSingle();
            for (int i = 0; i < bucketCount; i++)
                maxPeaks[i] = br.ReadSingle();
            DurationSeconds = duration;
            BucketCount = bucketCount;
            MinPeaks = minPeaks;
            MaxPeaks = maxPeaks;
            LoadedPath = audioFilePath;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Save waveform cache to .agxwf next to the audio file.</summary>
    public void SaveToAgxwf(string audioFilePath)
    {
        if (BucketCount == 0 || LoadedPath != audioFilePath) return;
        string wfPath = AgxwfPathForAudio(audioFilePath);
        try
        {
            using var fs = new FileStream(wfPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var bw = new BinaryWriter(fs);
            bw.Write(AgxwfMagic);
            bw.Write(AgxwfVersion);
            bw.Write(DurationSeconds);
            bw.Write(BucketCount);
            for (int i = 0; i < BucketCount; i++)
                bw.Write(MinPeaks[i]);
            for (int i = 0; i < BucketCount; i++)
                bw.Write(MaxPeaks[i]);
        }
        catch
        {
            // Best-effort save; ignore failure
        }
    }

    public bool Load(string filePath)
    {
        Clear();
        try
        {
            if (TryLoadFromAgxwf(filePath))
                return true;

            using var reader = new AudioFileReader(filePath);
            DurationSeconds = reader.TotalTime.TotalSeconds;
            if (DurationSeconds <= 0) return false;

            int sampleRate = reader.WaveFormat.SampleRate;
            int channels = reader.WaveFormat.Channels;
            long totalSamples = (long)(DurationSeconds * sampleRate * channels);
            int bucketCount = Math.Min(MaxBuckets, (int)Math.Max(1, DurationSeconds * 200)); // ~200 buckets/sec for smooth waveform

            var minPeaks = new float[bucketCount];
            var maxPeaks = new float[bucketCount];
            for (int i = 0; i < bucketCount; i++)
            {
                minPeaks[i] = 0;
                maxPeaks[i] = 0;
            }

            const int chunkSize = 8192;
            var buffer = new float[chunkSize];
            long samplesRead = 0;

            while (samplesRead < totalSamples)
            {
                int toRead = (int)Math.Min(chunkSize, totalSamples - samplesRead);
                int read = reader.Read(buffer, 0, toRead);
                if (read <= 0) break;

                for (int i = 0; i < read; i += channels)
                {
                    float peak = 0;
                    for (int c = 0; c < channels && i + c < read; c++)
                        peak = Math.Max(peak, Math.Abs(buffer[i + c]));
                    int bucket = (int)((samplesRead + i) / (double)totalSamples * bucketCount);
                    if (bucket >= bucketCount) bucket = bucketCount - 1;
                    if (peak > maxPeaks[bucket]) maxPeaks[bucket] = peak;
                    if (-peak < minPeaks[bucket]) minPeaks[bucket] = -peak;
                }
                samplesRead += read;
            }

            MinPeaks = minPeaks;
            MaxPeaks = maxPeaks;
            BucketCount = bucketCount;
            LoadedPath = filePath;
            SaveToAgxwf(filePath);
            return true;
        }
        catch
        {
            Clear();
            return false;
        }
    }

    public void Clear()
    {
        DurationSeconds = 0;
        BucketCount = 0;
        MinPeaks = Array.Empty<float>();
        MaxPeaks = Array.Empty<float>();
        LoadedPath = null;
    }

    /// <summary>Get min/max peak at a time. Linear interpolation between buckets for smooth display.</summary>
    public (float min, float max) GetPeakAtTime(double time)
    {
        if (BucketCount == 0 || DurationSeconds <= 0) return (0, 0);
        double t = Math.Clamp(time, 0, DurationSeconds);
        double frac = (t / DurationSeconds) * BucketCount;
        int i0 = (int)frac;
        if (i0 >= BucketCount - 1) return (MinPeaks[BucketCount - 1], MaxPeaks[BucketCount - 1]);
        i0 = Math.Max(0, i0);
        int i1 = i0 + 1;
        float f = (float)(frac - i0);
        float min = MinPeaks[i0] + f * (MinPeaks[i1] - MinPeaks[i0]);
        float max = MaxPeaks[i0] + f * (MaxPeaks[i1] - MaxPeaks[i0]);
        return (min, max);
    }
}
