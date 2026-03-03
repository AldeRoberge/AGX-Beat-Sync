using AGX_Beat_Sync.Editor;
using AGX_Beat_Sync.Input;
using AGX_Beat_Sync.Persistence;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.UI;

/// <summary>
/// In-game modal dialog for Open: recent projects list, Import music, Open project, Cancel.
/// Rendered with the same style as other panels. Browse actions are reported; game runs file dialog on STA.
/// </summary>
public class OpenDialogPanel
{
    private const int DialogWidth = 420;
    private const int DialogHeight = 440;
    private const int Margin = 20;
    private const int SectionGap = 12;
    private const int RowHeight = 24;
    private const int ButtonHeight = 28;
    private const int MaxRecentVisible = 8;
    private const int MaxPathDisplayChars = 52;

    private readonly List<string> _recentPaths = new();
    private Rectangle _dialogBounds;
    private Rectangle _recentListArea;
    private Rectangle _importMusicButton;
    private Rectangle _downloadFromUrlButton;
    private Rectangle _openProjectButton;
    private Rectangle _cancelButton;

    public GraphicsDevice? GraphicsDevice { get; set; }
    public InputManager? Input { get; set; }

    public bool IsVisible { get; set; }

    /// <summary>Set when user picks a recent project; game should load it and clear.</summary>
    public string? SelectedProjectPath { get; set; }

    /// <summary>Set when user clicks Import music; game should show file dialog then clear.</summary>
    public bool BrowseMusicRequested { get; set; }

    /// <summary>Set when user clicks Download from URL; game should show URL dialog then download and clear.</summary>
    public bool DownloadFromUrlRequested { get; set; }

    /// <summary>Set when user clicks Open project; game should show file dialog then clear.</summary>
    public bool BrowseProjectRequested { get; set; }

    public void Open()
    {
        _recentPaths.Clear();
        _recentPaths.AddRange(ProjectPersistence.GetRecentProjectPaths());
        SelectedProjectPath = null;
        BrowseMusicRequested = false;
        DownloadFromUrlRequested = false;
        BrowseProjectRequested = false;
        IsVisible = true;
    }

    public void ClearResult()
    {
        SelectedProjectPath = null;
        BrowseMusicRequested = false;
        DownloadFromUrlRequested = false;
        BrowseProjectRequested = false;
    }

    public void Update(GameTime gameTime, int viewportW, int viewportH)
    {
        if (!IsVisible || Input == null) return;

        _dialogBounds = new Rectangle(
            (viewportW - DialogWidth) / 2,
            (viewportH - DialogHeight) / 2,
            DialogWidth,
            DialogHeight);

        int y = _dialogBounds.Y + Margin;
        int contentW = DialogWidth - Margin * 2;

        // Recent list area (title + rows)
        y += 18; // "Recent projects" label
        _recentListArea = new Rectangle(_dialogBounds.X + Margin, y, contentW, RowHeight * Math.Min(MaxRecentVisible, Math.Max(1, _recentPaths.Count)));
        y += _recentListArea.Height + SectionGap;

        _importMusicButton = new Rectangle(_dialogBounds.X + Margin, y, contentW, ButtonHeight);
        y += ButtonHeight + 8;
        _downloadFromUrlButton = new Rectangle(_dialogBounds.X + Margin, y, contentW, ButtonHeight);
        y += ButtonHeight + 8;
        _openProjectButton = new Rectangle(_dialogBounds.X + Margin, y, contentW, ButtonHeight);
        y += ButtonHeight + SectionGap;
        _cancelButton = new Rectangle(_dialogBounds.Right - Margin - 90, y, 90, ButtonHeight);

        // Escape
        if (Input.IsKeyPressed(Keys.Escape))
        {
            IsVisible = false;
            return;
        }

        if (!Input.MouseLeftPressed) return;

        var mouse = Input.MousePosition;
        if (!_dialogBounds.Contains(mouse)) return;

        // Cancel
        if (_cancelButton.Contains(mouse))
        {
            IsVisible = false;
            return;
        }

        // Import music
        if (_importMusicButton.Contains(mouse))
        {
            BrowseMusicRequested = true;
            IsVisible = false;
            return;
        }

        // Download from URL
        if (_downloadFromUrlButton.Contains(mouse))
        {
            DownloadFromUrlRequested = true;
            IsVisible = false;
            return;
        }

        // Open project
        if (_openProjectButton.Contains(mouse))
        {
            BrowseProjectRequested = true;
            IsVisible = false;
            return;
        }

        // Recent list
        if (_recentListArea.Contains(mouse))
        {
            int index = (mouse.Y - _recentListArea.Y) / RowHeight;
            if (index >= 0 && index < _recentPaths.Count)
            {
                SelectedProjectPath = _recentPaths[index];
                IsVisible = false;
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!IsVisible || GraphicsDevice == null) return;

        var pixel = PanelBase.GetPixelTexture(GraphicsDevice);
        int vw = GraphicsDevice.Viewport.Width;
        int vh = GraphicsDevice.Viewport.Height;

        // Overlay
        spriteBatch.Draw(pixel, new Rectangle(0, 0, vw, vh), new Color(0, 0, 0, 180));

        // Dialog background
        spriteBatch.Draw(pixel, _dialogBounds, new Color(38, 40, 44));
        spriteBatch.Draw(pixel, new Rectangle(_dialogBounds.X, _dialogBounds.Y, _dialogBounds.Width, 1), new Color(60, 64, 72));
        spriteBatch.Draw(pixel, new Rectangle(_dialogBounds.X, _dialogBounds.Bottom - 1, _dialogBounds.Width, 1), new Color(60, 64, 72));
        spriteBatch.Draw(pixel, new Rectangle(_dialogBounds.X, _dialogBounds.Y, 1, _dialogBounds.Height), new Color(60, 64, 72));
        spriteBatch.Draw(pixel, new Rectangle(_dialogBounds.Right - 1, _dialogBounds.Y, 1, _dialogBounds.Height), new Color(60, 64, 72));

        int y = _dialogBounds.Y + Margin;
        var textColor = new Color(235, 235, 240);

        // Title
        InspectorDrawer.DrawLabel(spriteBatch, GraphicsDevice, _dialogBounds.X + Margin, y, "Open", pixel, textColor);
        y += 24;

        // Recent projects label
        InspectorDrawer.DrawLabel(spriteBatch, GraphicsDevice, _dialogBounds.X + Margin, y, "Recent projects", pixel, textColor);
        y += 20;

        // Recent list rows
        for (int i = 0; i < _recentPaths.Count && i < MaxRecentVisible; i++)
        {
            var rowRect = new Rectangle(_recentListArea.X, y, _recentListArea.Width, RowHeight);
            bool hover = rowRect.Contains(Input?.MousePosition ?? Point.Zero);
            spriteBatch.Draw(pixel, rowRect, hover ? new Color(52, 56, 62) : new Color(42, 45, 50));
            string display = _recentPaths[i];
            if (display.Length > MaxPathDisplayChars)
                display = display[..(MaxPathDisplayChars - 1)] + "…";
            InspectorDrawer.DrawLabel(spriteBatch, GraphicsDevice, rowRect.X + 6, rowRect.Y + 3, display, pixel, textColor);
            y += RowHeight;
        }

        y += SectionGap;

        // Import music button
        DrawButton(spriteBatch, pixel, _importMusicButton, "Import music…");
        y += ButtonHeight + 8;

        // Download from URL button
        DrawButton(spriteBatch, pixel, _downloadFromUrlButton, "Download from URL…");
        y += ButtonHeight + 8;

        // Open project button
        DrawButton(spriteBatch, pixel, _openProjectButton, "Open project file…");
        y += ButtonHeight + SectionGap;

        // Cancel button
        DrawButton(spriteBatch, pixel, _cancelButton, "Cancel");
    }

    private void DrawButton(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, string label)
    {
        bool hover = rect.Contains(Input?.MousePosition ?? Point.Zero);
        spriteBatch.Draw(pixel, rect, hover ? new Color(65, 70, 78) : new Color(52, 56, 62));
        spriteBatch.Draw(pixel, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2), new Color(70, 74, 82));
        InspectorDrawer.DrawLabel(spriteBatch, GraphicsDevice!, rect.X + 10, rect.Y + 5, label, pixel, new Color(235, 235, 240));
    }
}
