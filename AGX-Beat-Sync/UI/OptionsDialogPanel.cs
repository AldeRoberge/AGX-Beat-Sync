using AGX_Beat_Sync.Editor;
using AGX_Beat_Sync.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.UI;

/// <summary>
/// Reusable modal dialog with a title, message, and option buttons (e.g. Delete / Cancel).
/// Open with title, message, and options; when user clicks an option, dialog closes and SelectedOptionId is set.
/// </summary>
public class OptionsDialogPanel
{
    private const int Margin = 20;
    private const int SectionGap = 12;
    private const int ButtonHeight = 28;
    private const int MaxMessageWidth = 320;

    private string _title = "";
    private string _message = "";
    private List<(string Label, string Id)> _options = new();
    private Rectangle _dialogBounds;
    private readonly List<Rectangle> _optionRects = new();

    public GraphicsDevice? GraphicsDevice { get; set; }
    public InputManager? Input { get; set; }

    public bool IsVisible { get; set; }

    /// <summary>Set when user clicks an option; game should read then call ClearResult().</summary>
    public string? SelectedOptionId { get; set; }

    /// <summary>Show dialog with given title, message, and options (label, id).</summary>
    public void Open(string title, string message, IReadOnlyList<(string Label, string Id)> options)
    {
        _title = title;
        _message = message;
        _options = options.ToList();
        SelectedOptionId = null;
        IsVisible = true;
    }

    public void ClearResult()
    {
        SelectedOptionId = null;
    }

    public void Update(GameTime gameTime, int viewportW, int viewportH)
    {
        if (!IsVisible || Input == null) return;

        int contentW = MaxMessageWidth + Margin * 2;
        var wrappedLines = WrapMessage(_message, MaxMessageWidth);
        int messageHeight = wrappedLines.Count * 20;
        int buttonsHeight = _options.Count * (ButtonHeight + 8);
        int dialogHeight = Margin + 24 + SectionGap + messageHeight + SectionGap + buttonsHeight + Margin;

        _dialogBounds = new Rectangle(
            (viewportW - contentW) / 2,
            (viewportH - dialogHeight) / 2,
            contentW,
            dialogHeight);

        int y = _dialogBounds.Y + Margin + 24 + SectionGap + messageHeight + SectionGap;
        _optionRects.Clear();
        foreach (var _ in _options)
        {
            _optionRects.Add(new Rectangle(_dialogBounds.X + Margin, y, contentW - Margin * 2, ButtonHeight));
            y += ButtonHeight + 8;
        }

        if (Input.IsKeyPressed(Keys.Escape))
        {
            IsVisible = false;
            return;
        }

        if (!Input.MouseLeftPressed) return;

        var mouse = Input.MousePosition;
        if (!_dialogBounds.Contains(mouse)) return;

        for (int i = 0; i < _optionRects.Count && i < _options.Count; i++)
        {
            if (_optionRects[i].Contains(mouse))
            {
                SelectedOptionId = _options[i].Id;
                IsVisible = false;
                return;
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!IsVisible || GraphicsDevice == null) return;

        var pixel = PanelBase.GetPixelTexture(GraphicsDevice);
        int vw = GraphicsDevice.Viewport.Width;
        int vh = GraphicsDevice.Viewport.Height;

        spriteBatch.Draw(pixel, new Rectangle(0, 0, vw, vh), new Color(0, 0, 0, 180));

        spriteBatch.Draw(pixel, _dialogBounds, new Color(38, 40, 44));
        spriteBatch.Draw(pixel, new Rectangle(_dialogBounds.X, _dialogBounds.Y, _dialogBounds.Width, 1), new Color(60, 64, 72));
        spriteBatch.Draw(pixel, new Rectangle(_dialogBounds.X, _dialogBounds.Bottom - 1, _dialogBounds.Width, 1), new Color(60, 64, 72));
        spriteBatch.Draw(pixel, new Rectangle(_dialogBounds.X, _dialogBounds.Y, 1, _dialogBounds.Height), new Color(60, 64, 72));
        spriteBatch.Draw(pixel, new Rectangle(_dialogBounds.Right - 1, _dialogBounds.Y, 1, _dialogBounds.Height), new Color(60, 64, 72));

        int y = _dialogBounds.Y + Margin;
        var textColor = new Color(235, 235, 240);

        InspectorDrawer.DrawLabel(spriteBatch, GraphicsDevice, _dialogBounds.X + Margin, y, _title, pixel, textColor);
        y += 24 + SectionGap;

        if (!string.IsNullOrEmpty(_message))
        {
            foreach (var line in WrapMessage(_message, MaxMessageWidth))
            {
                InspectorDrawer.DrawLabel(spriteBatch, GraphicsDevice, _dialogBounds.X + Margin, y, line, pixel, textColor);
                y += 20;
            }
        }
        y += SectionGap;

        for (int i = 0; i < _optionRects.Count && i < _options.Count; i++)
        {
            DrawButton(spriteBatch, pixel, _optionRects[i], _options[i].Label);
        }
    }

    private static List<string> WrapMessage(string message, int maxWidthPx)
    {
        if (string.IsNullOrEmpty(message)) return new List<string>();
        // Simple wrap: assume ~8px per character for Segoe UI ~14pt, so maxChars ≈ maxWidthPx/8
        int maxChars = Math.Max(20, maxWidthPx / 8);
        var lines = new List<string>();
        var words = message.Split(' ');
        var current = new List<string>();
        int currentLen = 0;
        foreach (var w in words)
        {
            int add = (current.Count > 0 ? 1 : 0) + w.Length;
            if (currentLen + add > maxChars && current.Count > 0)
            {
                lines.Add(string.Join(" ", current));
                current.Clear();
                currentLen = 0;
            }
            current.Add(w);
            currentLen += (current.Count > 1 ? 1 : 0) + w.Length;
        }
        if (current.Count > 0)
            lines.Add(string.Join(" ", current));
        return lines;
    }

    private void DrawButton(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, string label)
    {
        bool hover = rect.Contains(Input?.MousePosition ?? Point.Zero);
        spriteBatch.Draw(pixel, rect, hover ? new Color(65, 70, 78) : new Color(52, 56, 62));
        spriteBatch.Draw(pixel, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2), new Color(70, 74, 82));
        InspectorDrawer.DrawLabel(spriteBatch, GraphicsDevice!, rect.X + 10, rect.Y + 5, label, pixel, new Color(235, 235, 240));
    }
}
