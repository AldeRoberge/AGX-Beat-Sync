using NAudio.Wave;

namespace AGX_Beat_Sync.Audio;

/// <summary>
/// Detects possible BPM values from an audio file using onset-strength envelope and autocorrelation.
/// </summary>
public static class BPMDetector
{
    // --- Analysis parameters ---
    private const int HopSize = 512;
    private const int FrameSize = 2048;
    private const double MinBPM = 55;
    private const double MaxBPM = 215;
    private const int MaxCandidates = 10;
    private const int PeakNeighborhood = 3;
    private const float MinPeakConfidence = 0.005f;
    private const double BPMDedupeTolerance = 2.0;

    // --- Fundamental-tempo preference (avoid 120 when true tempo is 90) ---
    private const double MinConfidenceRatioToPreferSlower = 0.5;
    private const double AlternativeBPMMinConfidence = 0.25;
    private static readonly TempoAlias[] TempoAliases =
    {
        new(115, 125, 82, 98),   // 120 → prefer 90
        new(172, 188, 82, 98),   // 180 → prefer 90
    };

    private readonly struct TempoAlias
    {
        public readonly double TopBpmMin;
        public readonly double TopBpmMax;
        public readonly double PreferBpmMin;
        public readonly double PreferBpmMax;

        public TempoAlias(double topMin, double topMax, double preferMin, double preferMax)
        {
            TopBpmMin = topMin;
            TopBpmMax = topMax;
            PreferBpmMin = preferMin;
            PreferBpmMax = preferMax;
        }
    }

    /// <summary>One possible BPM with a confidence score in [0,1].</summary>
    public readonly struct BPMCandidate
    {
        public double BPM { get; }
        public float Confidence { get; }

        public BPMCandidate(double bpm, float confidence)
        {
            BPM = bpm;
            Confidence = confidence;
        }
    }

    /// <summary>
    /// Analyze an audio file and return possible BPMs, best first.
    /// Optionally report progress in [0, 1] for UI loading bar.
    /// </summary>
    public static List<BPMCandidate> Detect(string filePath, IProgress<double>? progress = null)
    {
        var candidates = new List<BPMCandidate>();
        try
        {
            progress?.Report(0.05);
            using var reader = new AudioFileReader(filePath);
            int sampleRate = reader.WaveFormat.SampleRate;
            int channels = reader.WaveFormat.Channels;
            double durationSeconds = reader.TotalTime.TotalSeconds;

            if (!TryGetValidHopCount(durationSeconds, sampleRate, out int numHops))
                return candidates;

            progress?.Report(0.15);
            float[] onsetEnvelope = ComputeOnsetEnvelope(reader, sampleRate, channels, numHops);
            if (onsetEnvelope.Length == 0) return candidates;

            progress?.Report(0.45);
            (int minLag, int maxLag) = GetLagBounds(onsetEnvelope.Length, sampleRate);
            if (minLag >= maxLag) return candidates;

            float[] ac = Autocorrelate(onsetEnvelope, maxLag);
            progress?.Report(0.75);
            float ac0 = NormalizeFactor(ac[0]);

            CollectCandidatesFromPeaks(candidates, ac, ac0, minLag, maxLag, sampleRate);
            EnsureAtLeastOneCandidate(candidates, ac, ac0, minLag, maxLag, sampleRate);
            AddFundamentalAlternatives(candidates, ac, ac0, minLag, maxLag, sampleRate);

            candidates.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
            PreferFundamentalTempo(candidates);
            progress?.Report(1.0);
            return candidates;
        }
        catch
        {
            return new List<BPMCandidate>();
        }
    }

    // ─── Validation and bounds ─────────────────────────────────────────────

    private static bool TryGetValidHopCount(double durationSeconds, int sampleRate, out int numHops)
    {
        numHops = 0;
        if (durationSeconds < 1.5) return false;
        long totalMono = (long)(durationSeconds * sampleRate);
        numHops = (int)((totalMono - FrameSize) / HopSize);
        return numHops >= 48;
    }

    private static (int minLag, int maxLag) GetLagBounds(int envelopeLength, int sampleRate)
    {
        int minLag = Math.Max(2, LagFromBPM(MaxBPM, sampleRate));
        int maxLag = Math.Min(envelopeLength / 2, LagFromBPM(MinBPM, sampleRate));
        return (minLag, maxLag);
    }

    private static float NormalizeFactor(float ac0) => ac0 > 1e-10f ? ac0 : 1f;

    // ─── Candidate collection ──────────────────────────────────────────────

    private static void CollectCandidatesFromPeaks(
        List<BPMCandidate> candidates,
        float[] ac,
        float ac0,
        int minLag,
        int maxLag,
        int sampleRate)
    {
        var peaks = FindPeaks(ac, minLag, maxLag, PeakNeighborhood);
        foreach (var (lag, strength) in peaks.Take(MaxCandidates * 2))
        {
            double bpm = BPMFromLag(lag, sampleRate);
            if (bpm < MinBPM || bpm > MaxBPM) continue;

            double bpmNorm = FoldBPMToTypicalRange(bpm);
            float conf = (float)(strength / ac0);
            if (conf < MinPeakConfidence) continue;
            if (candidates.Any(c => Math.Abs(c.BPM - bpmNorm) < BPMDedupeTolerance)) continue;

            candidates.Add(new BPMCandidate(bpmNorm, conf));
            if (candidates.Count >= MaxCandidates) break;
        }
    }

    private static void EnsureAtLeastOneCandidate(
        List<BPMCandidate> candidates,
        float[] ac,
        float ac0,
        int minLag,
        int maxLag,
        int sampleRate)
    {
        if (candidates.Count > 0) return;

        int bestLag = minLag;
        float bestVal = 0;
        for (int i = minLag; i <= maxLag; i++)
        {
            if (ac[i] > bestVal)
            {
                bestVal = ac[i];
                bestLag = i;
            }
        }
        if (bestVal > 0)
        {
            double bpm = FoldBPMToTypicalRange(BPMFromLag(bestLag, sampleRate));
            candidates.Add(new BPMCandidate(bpm, (float)(bestVal / ac0)));
        }
    }

    /// <summary>
    /// When a candidate near 120 (or 180) exists, add 90 as an alternative using raw autocorrelation
    /// so that PreferFundamentalTempo can choose the true tempo.
    /// </summary>
    private static void AddFundamentalAlternatives(
        List<BPMCandidate> candidates,
        float[] ac,
        float ac0,
        int minLag,
        int maxLag,
        int sampleRate)
    {
        if (!candidates.Any(c => c.BPM >= 115 && c.BPM <= 125)) return;

        int lag90 = LagFromBPM(90, sampleRate);
        if (lag90 < minLag || lag90 > maxLag) return;
        if (candidates.Any(c => Math.Abs(c.BPM - 90) < BPMDedupeTolerance)) return;

        float conf90 = (float)(ac[lag90] / ac0);
        if (conf90 >= AlternativeBPMMinConfidence)
            candidates.Add(new BPMCandidate(90, conf90));
    }

    /// <summary>
    /// When the top candidate is a common harmonic of a slower tempo (e.g. 120 vs 90),
    /// prefer the slower candidate if its confidence is close enough.
    /// </summary>
    private static void PreferFundamentalTempo(List<BPMCandidate> candidates)
    {
        if (candidates.Count < 2) return;

        float topConf = candidates[0].Confidence;
        double topBpm = candidates[0].BPM;

        for (int i = 1; i < candidates.Count; i++)
        {
            if (candidates[i].Confidence < MinConfidenceRatioToPreferSlower * topConf)
                continue;

            foreach (var alias in TempoAliases)
            {
                if (topBpm >= alias.TopBpmMin && topBpm <= alias.TopBpmMax &&
                    candidates[i].BPM >= alias.PreferBpmMin && candidates[i].BPM <= alias.PreferBpmMax)
                {
                    Swap(candidates, 0, i);
                    return;
                }
            }
        }
    }

    private static void Swap(List<BPMCandidate> list, int i, int j)
    {
        (list[i], list[j]) = (list[j], list[i]);
    }

    // ─── Onset envelope ────────────────────────────────────────────────────

    /// <summary>Onset strength envelope: half-wave rectified frame-to-frame increase in energy.</summary>
    private static float[] ComputeOnsetEnvelope(AudioFileReader reader, int sampleRate, int channels, int numHops)
    {
        float[] mono = ReadMonoSamples(reader, sampleRate, channels);
        int n = mono.Length;
        if (n < FrameSize + (numHops - 1) * HopSize) return Array.Empty<float>();

        float[] energy = ComputeFrameEnergies(mono, numHops);
        return ComputeOnsetStrengths(energy);
    }

    private static float[] ReadMonoSamples(AudioFileReader reader, int sampleRate, int channels)
    {
        const int chunkSamples = 65536;
        var chunk = new float[chunkSamples];
        long totalMono = (long)(reader.TotalTime.TotalSeconds * sampleRate);
        int cap = (int)Math.Min(totalMono, 60 * sampleRate * 10);
        var mono = new List<float>(cap);
        int read;
        while ((read = reader.Read(chunk, 0, chunkSamples)) > 0)
        {
            for (int i = 0; i < read; i += channels)
            {
                float s = 0;
                for (int c = 0; c < channels && i + c < read; c++)
                    s += chunk[i + c];
                mono.Add(s / channels);
            }
        }
        return mono.ToArray();
    }

    private static float[] ComputeFrameEnergies(float[] mono, int numHops)
    {
        var energy = new float[numHops];
        for (int h = 0; h < numHops; h++)
        {
            int start = h * HopSize;
            double sum = 0;
            for (int i = 0; i < FrameSize && start + i < mono.Length; i++)
            {
                float s = mono[start + i];
                sum += s * s;
            }
            energy[h] = MathF.Sqrt((float)(sum / FrameSize));
        }
        return energy;
    }

    private static float[] ComputeOnsetStrengths(float[] energy)
    {
        var onset = new float[energy.Length];
        onset[0] = 0;
        for (int i = 1; i < energy.Length; i++)
            onset[i] = Math.Max(0, energy[i] - energy[i - 1]);
        return onset;
    }

    // ─── BPM ↔ lag conversion ─────────────────────────────────────────────

    private static int LagFromBPM(double bpm, int sampleRate)
    {
        double secondsPerBeat = 60.0 / bpm;
        double hopsPerBeat = secondsPerBeat * sampleRate / HopSize;
        return (int)Math.Round(hopsPerBeat);
    }

    private static double BPMFromLag(int lag, int sampleRate)
    {
        double secondsPerBeat = lag * HopSize / (double)sampleRate;
        return 60.0 / secondsPerBeat;
    }

    private static double FoldBPMToTypicalRange(double bpm)
    {
        while (bpm < 70) bpm *= 2;
        while (bpm > 175) bpm /= 2;
        return bpm;
    }

    // ─── Autocorrelation and peaks ──────────────────────────────────────────

    private static float[] Autocorrelate(float[] x, int maxLag)
    {
        int n = x.Length;
        var result = new float[maxLag + 1];
        for (int lag = 0; lag <= maxLag; lag++)
        {
            int count = n - lag;
            if (count <= 0) continue;
            double sum = 0;
            for (int i = 0; i < count; i++)
                sum += x[i] * x[i + lag];
            result[lag] = (float)(sum / count);
        }
        return result;
    }

    private static List<(int lag, float value)> FindPeaks(float[] ac, int minLag, int maxLag, int neighborhood)
    {
        var peaks = new List<(int lag, float value)>();
        for (int i = minLag; i <= maxLag; i++)
        {
            float v = ac[i];
            bool isPeak = true;
            for (int j = Math.Max(minLag, i - neighborhood); j <= Math.Min(maxLag, i + neighborhood); j++)
            {
                if (j == i) continue;
                if (ac[j] > v) { isPeak = false; break; }
            }
            if (isPeak && v > 0)
                peaks.Add((i, v));
        }
        peaks.Sort((a, b) => b.value.CompareTo(a.value));
        return peaks;
    }
}
