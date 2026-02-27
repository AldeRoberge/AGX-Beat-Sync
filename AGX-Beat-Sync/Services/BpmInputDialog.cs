using System.Windows.Forms;

namespace AGX_Beat_Sync.Services;

/// <summary>
/// Shows a simple dialog to type a BPM value. Runs on an STA thread.
/// Returns the entered string on OK, or null on Cancel.
/// </summary>
public static class BpmInputDialog
{
    public static string? Show(double currentBpm)
    {
        string? result = null;
        using var form = new Form
        {
            Text = "BPM",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            Width = 200,
            Height = 100,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var textBox = new TextBox
        {
            Text = ((int)Math.Round(currentBpm)).ToString(),
            Left = 12,
            Top = 12,
            Width = 160,
            Font = new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 12f)
        };
        textBox.SelectAll();
        var ok = new Button
        {
            Text = "OK",
            Left = 12,
            Top = 40,
            Width = 75,
            DialogResult = DialogResult.OK
        };
        var cancel = new Button
        {
            Text = "Cancel",
            Left = 92,
            Top = 40,
            Width = 75,
            DialogResult = DialogResult.Cancel
        };
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.Controls.Add(textBox);
        form.Controls.Add(ok);
        form.Controls.Add(cancel);
        if (form.ShowDialog() == DialogResult.OK)
            result = textBox.Text;
        return result;
    }
}
