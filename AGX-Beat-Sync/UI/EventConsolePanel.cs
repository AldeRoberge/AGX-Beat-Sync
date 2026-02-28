using System.Collections.Generic;
using System.Windows.Forms;
using AGX_Beat_Sync.Editor;
using AGX_Beat_Sync.Input;
using AGX_Beat_Sync.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.UI;

/// <summary>
/// Console panel: Engine logs (ILogger) and timeline Events. Toggle "Engine" / "Events" to filter.
/// </summary>
public class EventConsolePanel : PanelBase
{
    private const int LineHeight = 18;
    private const int Padding = 6;
    private const int ScrollbarWidth = 10;
    private const int MinScrollbarThumbHeight = 20;
    private const int MaxLogEntries = 500;
    private const int HeaderToggleHeight = 20;
    private const int HeaderTogglePadding = 6;
    private const int ToggleChipPaddingH = 10;
    private const int ToggleChipPaddingV = 4;

    private readonly List<string> _eventEntries = new();
    private readonly object _entriesLock = new();
    private int _scrollY;
    private int _contentHeight;
    private bool _scrollbarThumbDragging;
    private int _thumbDragStartY;
    private int _scrollStartY;
    private int _selectedIndex = -1;
    private volatile bool _userAtBottom = true;

    /// <summary>When true, engine logs (ILogger) are shown in the console.</summary>
    public bool ShowEngine { get; set; } = true;
    /// <summary>When true, timeline events (e.g. Spawn Entity) are shown.</summary>
    public bool ShowEvents { get; set; } = true;

    // Combined lines for selection/copy: (display text, color, isEvent, eventIndex or -1). Rebuilt each frame from toggles.
    private readonly List<(string Text, Color Color, bool IsEvent, int EventIndex)> _displayLines = new();

    public EventConsolePanel()
    {
        Title = "Console";
        BackgroundColor = new Color(28, 30, 34);
        HeaderColor = new Color(38, 41, 47);
        HeaderHeight = 28;
    }

    /// <summary>Log an event fired by the system (thread-safe).</summary>
    public void LogEvent(double timeSeconds, string trackName, string message)
    {
        string timeStr = TimeFormatHelper.Format(timeSeconds);
        string line = $"{timeStr} | {trackName} | {message}";
        lock (_entriesLock)
        {
            _eventEntries.Add(line);
            if (_eventEntries.Count > MaxLogEntries)
            {
                _eventEntries.RemoveAt(0);
                if (_selectedIndex >= 0 && _selectedIndex < _displayLines.Count && _displayLines[_selectedIndex].EventIndex > 0)
                    _selectedIndex--;
                else if (_selectedIndex == 0) _selectedIndex = -1;
            }
        }
        if (_userAtBottom)
            _scrollY = int.MaxValue;
    }

    /// <summary>Clear event entries and optionally engine logs (e.g. when starting playback).</summary>
    public void Clear(bool clearEngineLogs = true)
    {
        lock (_entriesLock)
            _eventEntries.Clear();
        if (clearEngineLogs)
            EngineLogs.Clear();
        _scrollY = 0;
        _selectedIndex = -1;
        _userAtBottom = true;
    }

    /// <summary>Copy the selected log entry to the clipboard. Returns true if something was copied.</summary>
    public bool CopySelectionToClipboard()
    {
        EnsureDisplayLines();
        if (_selectedIndex < 0 || _selectedIndex >= _displayLines.Count) return false;
        string toCopy = _displayLines[_selectedIndex].Text;
        if (string.IsNullOrEmpty(toCopy)) return false;
        try
        {
            Clipboard.SetText(toCopy);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Remove the selected log entry (Events only). Returns true if an entry was removed.</summary>
    public bool RemoveSelectedEntry()
    {
        EnsureDisplayLines();
        if (_selectedIndex < 0 || _selectedIndex >= _displayLines.Count) return false;
        var line = _displayLines[_selectedIndex];
        if (!line.IsEvent || line.EventIndex < 0) return false;
        lock (_entriesLock)
        {
            if (line.EventIndex < _eventEntries.Count)
            {
                _eventEntries.RemoveAt(line.EventIndex);
                _selectedIndex = -1;
                return true;
            }
        }
        return false;
    }

    public InputManager? Input { get; set; }

    private void EnsureDisplayLines()
    {
        var engineSnapshot = ShowEngine ? EngineLogs.GetSnapshot() : (IReadOnlyList<EngineLogEntry>)System.Array.Empty<EngineLogEntry>();
        List<string> eventSnapshot;
        lock (_entriesLock)
            eventSnapshot = ShowEvents ? new List<string>(_eventEntries) : new List<string>();

        _displayLines.Clear();

        foreach (var e in engineSnapshot)
        {
            string text = $"{e.TimeString} | [{e.Level}] {e.Message}";
            Color color = LevelColor(e.Level);
            _displayLines.Add((text, color, false, -1));
        }
        for (int i = 0; i < eventSnapshot.Count; i++)
        {
            _displayLines.Add((eventSnapshot[i], new Color(200, 210, 220), true, i));
        }
    }

    private static Color LevelColor(string level)
    {
        return level switch
        {
            "VRB" => new Color(120, 125, 132),
            "DBG" => new Color(100, 180, 220),
            "INF" => new Color(220, 225, 230),
            "WRN" => new Color(230, 180, 80),
            "ERR" => new Color(255, 110, 110),
            "FTL" => new Color(200, 70, 70),
            _ => new Color(180, 185, 192)
        };
    }

    public override void Update(GameTime gameTime)
    {
        if (Input == null) return;

        EnsureDisplayLines();
        var content = ContentBounds;
        int contentWidth = Math.Max(0, content.Width - ScrollbarWidth);
        int visibleHeight = content.Height;
        _contentHeight = _displayLines.Count * LineHeight;

        int maxScroll = Math.Max(0, _contentHeight - visibleHeight);
        _scrollY = Math.Clamp(_scrollY, 0, maxScroll);

        // Header toggle hit-test
        var header = HeaderBounds;
        int toggleY = header.Y + (header.Height - HeaderToggleHeight) / 2;
        int right = header.Right - HeaderTogglePadding;
        int engineRight = right - ToggleChipPaddingH * 2 - 44;
        int eventsRight = right - ToggleChipPaddingH * 2 - 28;
        var engineToggleRect = new Rectangle(engineRight - 52, toggleY, 52, HeaderToggleHeight);
        var eventsToggleRect = new Rectangle(eventsRight - 44, toggleY, 44, HeaderToggleHeight);

        if (Input.MouseLeftPressed && header.Contains(Input.MousePosition))
        {
            if (engineToggleRect.Contains(Input.MousePosition))
                ShowEngine = !ShowEngine;
            else if (eventsToggleRect.Contains(Input.MousePosition))
                ShowEvents = !ShowEvents;
        }

        if (_scrollbarThumbDragging)
        {
            if (Input.MouseLeftDown)
            {
                var scrollbar = GetScrollbarBounds(content);
                int thumbHeight = GetThumbHeight(content);
                int travel = scrollbar.Height - thumbHeight;
                if (travel > 0 && maxScroll > 0)
                {
                    int deltaY = Input.MousePosition.Y - _thumbDragStartY;
                    int scrollDelta = (int)(deltaY / (double)travel * maxScroll);
                    _scrollY = Math.Clamp(_scrollStartY + scrollDelta, 0, maxScroll);
                }
            }
            else
                _scrollbarThumbDragging = false;
        }
        else if (ContainsPoint(Input.MousePosition))
        {
            if (Input.ScrollWheelDelta != 0)
            {
                _scrollY -= Input.ScrollWheelDelta;
                _scrollY = Math.Clamp(_scrollY, 0, maxScroll);
            }
            if (Input.MouseLeftPressed)
            {
                var scrollbar = GetScrollbarBounds(content);
                if (scrollbar.Contains(Input.MousePosition))
                {
                    _scrollbarThumbDragging = true;
                    _thumbDragStartY = Input.MousePosition.Y;
                    _scrollStartY = _scrollY;
                }
                else
                {
                    var logArea = new Rectangle(content.X, content.Y, contentWidth, content.Height);
                    if (logArea.Contains(Input.MousePosition))
                    {
                        int lineIndex = (Input.MousePosition.Y - content.Y + _scrollY) / LineHeight;
                        if (lineIndex >= 0 && lineIndex < _displayLines.Count)
                            _selectedIndex = lineIndex;
                        else
                            _selectedIndex = -1;
                    }
                }
            }

            bool ctrl = Input.IsKeyDown(Keys.LeftControl) || Input.IsKeyDown(Keys.RightControl);
            if (ContainsPoint(Input.MousePosition) && ctrl && Input.IsKeyPressed(Keys.C))
            {
                if (_selectedIndex >= 0 && _selectedIndex < _displayLines.Count)
                {
                    string toCopy = _displayLines[_selectedIndex].Text;
                    if (!string.IsNullOrEmpty(toCopy))
                    {
                        try { Clipboard.SetText(toCopy); } catch { }
                    }
                }
            }
        }

        _userAtBottom = maxScroll <= 0 || _scrollY >= maxScroll;
    }

    protected override void DrawPanelBackground(SpriteBatch spriteBatch)
    {
        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);
        spriteBatch.Draw(pixel, Bounds, BackgroundColor);
        spriteBatch.Draw(pixel, HeaderBounds, HeaderColor);
        DrawBorder(spriteBatch, pixel);

        var device = spriteBatch.GraphicsDevice;
        var header = HeaderBounds;
        int titleX = header.X + Padding;
        int titleY = header.Y + (header.Height - InspectorDrawer.RowHeight) / 2;
        InspectorDrawer.DrawLabel(spriteBatch, device, titleX, titleY + 2, Title, pixel, new Color(230, 232, 238));

        int toggleY = header.Y + (header.Height - HeaderToggleHeight) / 2;
        int right = header.Right - HeaderTogglePadding;
        int x = right;

        x -= ToggleChipPaddingH + 44;
        var eventsRect = new Rectangle(x, toggleY, 44 + ToggleChipPaddingH * 2, HeaderToggleHeight);
        bool eventsOn = ShowEvents;
        Color eventsBg = eventsOn ? new Color(65, 115, 165) : new Color(55, 58, 65);
        spriteBatch.Draw(pixel, eventsRect, eventsBg);
        InspectorDrawer.DrawLabel(spriteBatch, device, x + ToggleChipPaddingH, toggleY + ToggleChipPaddingV, "Events", pixel, eventsOn ? new Color(255, 255, 255) : new Color(160, 165, 175));

        x -= ToggleChipPaddingH + 52 + 4;
        var engineRect = new Rectangle(x, toggleY, 52 + ToggleChipPaddingH * 2, HeaderToggleHeight);
        bool engineOn = ShowEngine;
        Color engineBg = engineOn ? new Color(70, 130, 100) : new Color(55, 58, 65);
        spriteBatch.Draw(pixel, engineRect, engineBg);
        InspectorDrawer.DrawLabel(spriteBatch, device, x + ToggleChipPaddingH, toggleY + ToggleChipPaddingV, "Engine", pixel, engineOn ? new Color(255, 255, 255) : new Color(160, 165, 175));
    }

    protected override void DrawContent(SpriteBatch spriteBatch)
    {
        var content = ContentBounds;
        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);
        var device = spriteBatch.GraphicsDevice;
        int contentWidth = Math.Max(0, content.Width - ScrollbarWidth);
        var logArea = new Rectangle(content.X, content.Y, contentWidth, content.Height);

        EnsureDisplayLines();

        if (_displayLines.Count == 0)
        {
            string hint = !ShowEngine && !ShowEvents
                ? "Enable Engine or Events above to see logs."
                : !ShowEvents
                    ? "No engine logs yet. Use EngineLogs.Logger to log."
                    : !ShowEngine
                        ? "No events yet. Press Play to trigger timeline events."
                        : "No logs yet. Press Play for events; use EngineLogs.Logger for engine logs.";
            InspectorDrawer.DrawLabel(spriteBatch, device, logArea.X + Padding, logArea.Y + Padding, hint, pixel, new Color(130, 135, 142));
            return;
        }

        int logAreaWidth = Math.Max(0, content.Width - ScrollbarWidth);
        int selectedIndex = _selectedIndex;

        int y = content.Y - _scrollY;
        for (int i = 0; i < _displayLines.Count; i++)
        {
            int lineBottom = y + LineHeight;
            if (lineBottom > content.Y && y < content.Bottom)
            {
                if (i == selectedIndex)
                {
                    var highlightRect = new Rectangle(content.X, y, logAreaWidth, LineHeight);
                    spriteBatch.Draw(pixel, highlightRect, new Color(55, 62, 75));
                }
                var line = _displayLines[i];
                InspectorDrawer.DrawLabel(spriteBatch, device, content.X + Padding, y + 2, line.Text, pixel, line.Color);
            }
            y = lineBottom;
        }

        if (_contentHeight > content.Height && content.Width > ScrollbarWidth)
        {
            var scrollbar = GetScrollbarBounds(content);
            spriteBatch.Draw(pixel, scrollbar, new Color(42, 45, 50));
            var thumb = GetThumbBounds(content, Math.Max(0, _contentHeight - content.Height));
            spriteBatch.Draw(pixel, thumb, new Color(65, 70, 78));
            if (thumb.Height >= 10)
            {
                var gripColor = new Color(50, 54, 62);
                int gripLeft = thumb.X + 2;
                int gripW = Math.Max(1, thumb.Width - 4);
                int centerY = thumb.Y + thumb.Height / 2;
                spriteBatch.Draw(pixel, new Rectangle(gripLeft, centerY - 2, gripW, 1), gripColor);
                spriteBatch.Draw(pixel, new Rectangle(gripLeft, centerY + 2, gripW, 1), gripColor);
            }
        }
    }

    private Rectangle GetScrollbarBounds(Rectangle content)
    {
        return new Rectangle(content.Right - ScrollbarWidth, content.Y, ScrollbarWidth, content.Height);
    }

    private int GetThumbHeight(Rectangle content)
    {
        if (_contentHeight <= 0) return content.Height;
        return Math.Max(MinScrollbarThumbHeight, (int)(content.Height / (double)_contentHeight * content.Height));
    }

    private Rectangle GetThumbBounds(Rectangle content, int maxScroll)
    {
        var scrollbar = GetScrollbarBounds(content);
        if (_contentHeight <= 0 || content.Height <= 0)
            return new Rectangle(scrollbar.X, scrollbar.Y, scrollbar.Width, Math.Min(scrollbar.Height, MinScrollbarThumbHeight));
        int thumbHeight = Math.Max(MinScrollbarThumbHeight, (int)(content.Height / (double)_contentHeight * scrollbar.Height));
        int travel = scrollbar.Height - thumbHeight;
        int thumbY = travel > 0 && maxScroll > 0
            ? scrollbar.Y + (int)(_scrollY / (double)maxScroll * travel)
            : scrollbar.Y;
        return new Rectangle(scrollbar.X, thumbY, scrollbar.Width, thumbHeight);
    }
}
