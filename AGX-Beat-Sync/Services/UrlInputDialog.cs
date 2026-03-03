using System.Windows.Forms;

namespace AGX_Beat_Sync.Services;

/// <summary>
/// Shows a simple dialog to enter a YouTube or SoundCloud URL. Runs on an STA thread.
/// Returns the entered URL on OK, or null on Cancel.
/// </summary>
public static class UrlInputDialog
{
    public static string? Show()
    {
        string? result = null;
        using var form = new Form
        {
            Text = "Download from URL",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            Width = 420,
            Height = 120,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var label = new Label
        {
            Text = "YouTube or SoundCloud URL:",
            Left = 12,
            Top = 12,
            AutoSize = true
        };
        var textBox = new TextBox
        {
            Left = 12,
            Top = 32,
            Width = 380,
            Font = new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 10f),
            PlaceholderText = "https://..."
        };
        var ok = new Button
        {
            Text = "Download",
            Left = 12,
            Top = 58,
            Width = 90,
            DialogResult = DialogResult.OK
        };
        var cancel = new Button
        {
            Text = "Cancel",
            Left = 108,
            Top = 58,
            Width = 75,
            DialogResult = DialogResult.Cancel
        };
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.Controls.Add(label);
        form.Controls.Add(textBox);
        form.Controls.Add(ok);
        form.Controls.Add(cancel);
        if (form.ShowDialog() == DialogResult.OK)
        {
            var url = textBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(url))
                result = url;
        }
        return result;
    }
}
