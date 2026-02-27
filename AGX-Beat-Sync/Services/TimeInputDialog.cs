using System.Windows.Forms;

namespace AGX_Beat_Sync.Services;

/// <summary>
/// Dialog to type a time in HH:mm:ss:frame format and seek. Returns total seconds on OK, null on Cancel.
/// </summary>
public static class TimeInputDialog
{
    public static double? Show(double currentSeconds, int fps = TimeFormatHelper.DefaultFramesPerSecond)
    {
        double? result = null;
        string current = TimeFormatHelper.Format(currentSeconds, fps);
        using var form = new Form
        {
            Text = "Go to time (HH:mm:ss:frame)",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            Width = 280,
            Height = 120,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var textBox = new TextBox
        {
            Text = current,
            Left = 12,
            Top = 12,
            Width = 240,
            Font = new System.Drawing.Font(System.Drawing.FontFamily.GenericMonospace, 11f)
        };
        textBox.SelectAll();
        var ok = new Button
        {
            Text = "OK",
            Left = 12,
            Top = 42,
            Width = 75,
            DialogResult = DialogResult.OK
        };
        form.AcceptButton = ok;
        form.Controls.Add(textBox);
        form.Controls.Add(ok);
        if (form.ShowDialog() == DialogResult.OK)
        {
            var parsed = TimeFormatHelper.Parse(textBox.Text, fps);
            if (parsed.HasValue && parsed.Value >= 0)
                result = parsed.Value;
        }
        return result;
    }
}
