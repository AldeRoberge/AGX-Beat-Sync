using System.Globalization;

namespace AGX_Beat_Sync.Services;

/// <summary>
/// Premiere Pro-style time: HH:mm:ss:frame (frames 0 to fps-1 per second).
/// </summary>
public static class TimeFormatHelper
{
    public const int DefaultFramesPerSecond = 30;

    /// <summary>Format seconds as HH:mm:ss:ff (zero-padded).</summary>
    public static string Format(double totalSeconds, int fps = DefaultFramesPerSecond)
    {
        totalSeconds = Math.Max(0, totalSeconds);
        int h = (int)(totalSeconds / 3600);
        int m = (int)((totalSeconds % 3600) / 60);
        int s = (int)(totalSeconds % 60);
        int frame = (int)Math.Floor((totalSeconds - Math.Floor(totalSeconds)) * fps) % fps;
        return $"{h:D2}:{m:D2}:{s:D2}:{frame:D2}";
    }

    /// <summary>Parse HH:mm:ss:ff or H:m:s:f to total seconds. Returns null if invalid.</summary>
    public static double? Parse(string input, int fps = DefaultFramesPerSecond)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        string[] parts = input.Trim().Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4) return null;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int h) || h < 0) return null;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int m) || m < 0 || m > 59) return null;
        if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int s) || s < 0 || s > 59) return null;
        if (!int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int f) || f < 0 || f >= fps) return null;
        return h * 3600 + m * 60 + s + f / (double)fps;
    }
}
