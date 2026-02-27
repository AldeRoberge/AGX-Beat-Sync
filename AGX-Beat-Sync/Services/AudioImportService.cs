using System.Windows.Forms;

namespace AGX_Beat_Sync.Services;

/// <summary>
/// Shows a file dialog to pick an audio file (MP3/WAV). Returns the chosen path or null.
/// </summary>
public static class AudioImportService
{
    public static string? PickAudioFile()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Import audio",
            Filter = "Audio files (*.mp3;*.wav)|*.mp3;*.wav|MP3 (*.mp3)|*.mp3|WAV (*.wav)|*.wav|All files (*.*)|*.*",
            FilterIndex = 1
        };
        return dlg.ShowDialog() == DialogResult.OK ? dlg.FileName : null;
    }
}
