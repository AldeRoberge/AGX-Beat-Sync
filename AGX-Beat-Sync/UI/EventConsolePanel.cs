using System.Collections.Generic;
using System.Windows.Forms;
using AGX_Beat_Sync.Editor;
using AGX_Beat_Sync.Input;
using AGX_Beat_Sync.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.UI;

/// <summary>Minimum level for engine log display (show this level and above).</summary>
public enum EngineLogLevelFilter
{
    Verbose,
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}

/// <summary>
/// Console panel: Engine logs (ILogger) and timeline Events. Toggle "Engine" / "Events" to filter.
/// </summary>
public class EventConsolePanel : PanelBase
{
    private const int LineHeight = 18;
    private const int Padding = 6;
    private const int ScrollbarWidth = 10;
    private const int ShortcutsLineHeight = 16;
    private const string ShortcutsText = "Ctrl+Z Undo  Ctrl+Shift+Z Redo  Ctrl+C Copy  Ctrl+X Cut  Ctrl+A Select all";
    private const int MinScrollbarThumbHeight = 20;
    private const int MaxLogEntries = 500;
    private const int HeaderToggleHeight = 20;
    private const int HeaderTogglePadding = 6;
    private const int ToggleChipPaddingH = 10;
    private const int ToggleChipPaddingV = 4;
    private const int ContextMenuWidth = 100;

    private readonly List<string> _eventEntries = new();
    private readonly object _entriesLock = new();
    private int _scrollY;
    private int _scrollX;
    private int _contentHeight;
    private int _contentWidth;
    private bool _scrollbarThumbDragging;
    private int _thumbDragStartY;
    private int _scrollStartY;
    private int _selectedIndex = -1;
    private bool _selectAll;
    private volatile bool _userAtBottom = true;

    /// <summary>When true, engine logs (ILogger) are shown in the console.</summary>
    public bool ShowEngine { get; set; } = true;
    /// <summary>When true, timeline events (e.g. Spawn Entity) are shown.</summary>
    public bool ShowEvents { get; set; } = true;
    /// <summary>Minimum engine log level to show (e.g. Info = show Info, Warning, Error, Fatal).</summary>
    public EngineLogLevelFilter EngineMinLogLevel { get; set; } = EngineLogLevelFilter.Info;

    private bool _levelDropdownOpen;
    private static readonly string[] LevelFilterOptionLabels = { "All", "DBG", "INF", "WRN", "ERR", "FTL" };

    // In-game context menu (MonoGame style)
    private bool _contextMenuOpen;
    private int _contextMenuX;
    private int _contextMenuY;
    private string? _contextMenuLineToCopy;

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
        _scrollX = 0;
        _selectedIndex = -1;
        _selectAll = false;
        _contextMenuOpen = false;
        _userAtBottom = true;
    }

    /// <summary>Copy the selected log entry (or all if Select All was used) to the clipboard. Returns true if something was copied.</summary>
    public bool CopySelectionToClipboard()
    {
        EnsureDisplayLines();
        if (_selectAll && _displayLines.Count > 0)
        {
            _selectAll = false;
            try
            {
                Clipboard.SetText(string.Join("\r\n", System.Linq.Enumerable.Select(_displayLines, d => d.Text)));
                return true;
            }
            catch { return false; }
        }
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

    /// <summary>Select all console lines; next Copy will copy entire content.</summary>
    public void SelectAll()
    {
        _selectAll = true;
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

    private static void CopyLineToClipboard(string text)
    {
        try { Clipboard.SetText(text); } catch { }
    }

    private void EnsureDisplayLines()
    {
        var engineSnapshot = ShowEngine ? EngineLogs.GetSnapshot() : (IReadOnlyList<EngineLogEntry>)System.Array.Empty<EngineLogEntry>();
        List<string> eventSnapshot;
        lock (_entriesLock)
            eventSnapshot = ShowEvents ? new List<string>(_eventEntries) : new List<string>();

        _displayLines.Clear();

        foreach (var e in engineSnapshot)
        {
            if (!PassesLevelFilter(e.Level, EngineMinLogLevel)) continue;
            string text = $"{e.TimeString} | [{e.Level}] {e.Message}";
            Color color = LevelColor(e.Level);
            _displayLines.Add((text, color, false, -1));
        }
        for (int i = 0; i < eventSnapshot.Count; i++)
        {
            _displayLines.Add((eventSnapshot[i], new Color(200, 210, 220), true, i));
        }
    }

    private static int LevelOrder(string level)
    {
        return level switch { "VRB" => 0, "DBG" => 1, "INF" => 2, "WRN" => 3, "ERR" => 4, "FTL" => 5, _ => 2 };
    }

    private static int LevelOrder(EngineLogLevelFilter filter)
    {
        return (int)filter;
    }

    private static bool PassesLevelFilter(string level, EngineLogLevelFilter minLevel)
    {
        return LevelOrder(level) >= LevelOrder(minLevel);
    }

    private static string LevelFilterLabel(EngineLogLevelFilter filter)
    {
        return filter switch
        {
            EngineLogLevelFilter.Verbose => "All",
            EngineLogLevelFilter.Debug => "DBG",
            EngineLogLevelFilter.Info => "INF",
            EngineLogLevelFilter.Warning => "WRN",
            EngineLogLevelFilter.Error => "ERR",
            EngineLogLevelFilter.Fatal => "FTL",
            _ => "INF"
        };
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
        int logVisibleHeight = content.Height - ShortcutsLineHeight;
        var logContentRect = new Rectangle(content.X, content.Y + ShortcutsLineHeight, content.Width, logVisibleHeight);
        _contentHeight = _displayLines.Count * LineHeight;

        // Compute max line width for horizontal scroll
        _contentWidth = Padding;
        foreach (var d in _displayLines)
        {
            var (w, _) = InspectorDrawer.MeasureLabel(d.Text);
            _contentWidth = Math.Max(_contentWidth, Padding + w);
        }
        int maxScrollX = Math.Max(0, _contentWidth - contentWidth);
        _scrollX = Math.Clamp(_scrollX, 0, maxScrollX);

        int maxScroll = Math.Max(0, _contentHeight - logVisibleHeight);
        _scrollY = Math.Clamp(_scrollY, 0, maxScroll);

        // Header toggle hit-test (same rects as drawn in DrawPanelBackground)
        var header = HeaderBounds;
        int toggleY = header.Y + (header.Height - HeaderToggleHeight) / 2;
        int right = header.Right - HeaderTogglePadding;
        int levelChipW = 36 + ToggleChipPaddingH * 2; // "All" / "WRN" etc.
        int xEvents = right - (ToggleChipPaddingH + 44);
        int xEngine = xEvents - (ToggleChipPaddingH + 52 + 4);
        int xLevel = xEngine - (levelChipW + 4);
        var eventsToggleRect = new Rectangle(xEvents, toggleY, 44 + ToggleChipPaddingH * 2, HeaderToggleHeight);
        var engineToggleRect = new Rectangle(xEngine, toggleY, 52 + ToggleChipPaddingH * 2, HeaderToggleHeight);
        var levelToggleRect = new Rectangle(xLevel, toggleY, levelChipW, HeaderToggleHeight);

        // Level dropdown: when open, handle option click or click-outside to close first
        int dropdownH = LevelFilterOptionLabels.Length * InspectorDrawer.RowHeight;
        var levelDropdownRect = new Rectangle(xLevel, toggleY + HeaderToggleHeight + 2, levelChipW, dropdownH);
        if (_levelDropdownOpen && Input.MouseLeftPressed)
        {
            bool hitOption = false;
            for (int i = 0; i < LevelFilterOptionLabels.Length; i++)
            {
                var rowRect = new Rectangle(levelDropdownRect.X, levelDropdownRect.Y + i * InspectorDrawer.RowHeight, levelDropdownRect.Width, InspectorDrawer.RowHeight);
                if (rowRect.Contains(Input.MousePosition))
                {
                    EngineMinLogLevel = (EngineLogLevelFilter)i;
                    _levelDropdownOpen = false;
                    hitOption = true;
                    break;
                }
            }
            if (!hitOption)
            {
                if (levelToggleRect.Contains(Input.MousePosition))
                    _levelDropdownOpen = false;
                else if (engineToggleRect.Contains(Input.MousePosition))
                {
                    ShowEngine = !ShowEngine;
                    _levelDropdownOpen = false;
                }
                else if (eventsToggleRect.Contains(Input.MousePosition))
                {
                    ShowEvents = !ShowEvents;
                    _levelDropdownOpen = false;
                }
                else if (!levelDropdownRect.Contains(Input.MousePosition) && ContainsPoint(Input.MousePosition))
                    _levelDropdownOpen = false;
            }
        }
        else if (Input.MouseLeftPressed && header.Contains(Input.MousePosition))
        {
            if (levelToggleRect.Contains(Input.MousePosition))
                _levelDropdownOpen = !_levelDropdownOpen;
            else if (engineToggleRect.Contains(Input.MousePosition))
                ShowEngine = !ShowEngine;
            else if (eventsToggleRect.Contains(Input.MousePosition))
                ShowEvents = !ShowEvents;
        }

        if (_scrollbarThumbDragging)
        {
            if (Input.MouseLeftDown)
            {
                var scrollbar = GetScrollbarBounds(logContentRect);
                int thumbHeight = GetThumbHeight(logContentRect);
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
                bool shift = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
                if (shift)
                {
                    _scrollX += Input.ScrollWheelDelta * 40;
                    _scrollX = Math.Clamp(_scrollX, 0, maxScrollX);
                }
                else
                {
                    _scrollY -= Input.ScrollWheelDelta;
                    _scrollY = Math.Clamp(_scrollY, 0, maxScroll);
                }
            }
            if (Input.MouseLeftPressed)
            {
                var scrollbar = GetScrollbarBounds(logContentRect);
                if (scrollbar.Contains(Input.MousePosition))
                {
                    _scrollbarThumbDragging = true;
                    _thumbDragStartY = Input.MousePosition.Y;
                    _scrollStartY = _scrollY;
                }
                else
                {
                    if (logContentRect.Contains(Input.MousePosition))
                    {
                        int lineIndex = (Input.MousePosition.Y - logContentRect.Y + _scrollY) / LineHeight;
                        if (lineIndex >= 0 && lineIndex < _displayLines.Count)
                        {
                            _selectedIndex = lineIndex;
                            _selectAll = false;
                        }
                        else
                            _selectedIndex = -1;
                    }
                }
            }

            if (Input.MouseRightPressed && logContentRect.Contains(Input.MousePosition))
            {
                int lineIndex = (Input.MousePosition.Y - logContentRect.Y + _scrollY) / LineHeight;
                bool hasLine = lineIndex >= 0 && lineIndex < _displayLines.Count;
                _contextMenuOpen = true;
                _contextMenuX = Input.MousePosition.X;
                _contextMenuY = Input.MousePosition.Y;
                _contextMenuLineToCopy = hasLine && !string.IsNullOrEmpty(_displayLines[lineIndex].Text) ? _displayLines[lineIndex].Text : null;
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

        if (_contextMenuOpen && Input.MouseLeftPressed)
        {
            int menuH = 2 * InspectorDrawer.RowHeight;
            int mx = _contextMenuX;
            int my = _contextMenuY;
            if (mx + ContextMenuWidth > Bounds.Right) mx = Bounds.Right - ContextMenuWidth;
            if (my + menuH > Bounds.Bottom) my = Bounds.Bottom - menuH;
            if (mx < Bounds.X) mx = Bounds.X;
            if (my < Bounds.Y) my = Bounds.Y;
            var menuRect = new Rectangle(mx, my, ContextMenuWidth, menuH);
            var copyRect = new Rectangle(mx, my, ContextMenuWidth, InspectorDrawer.RowHeight);
            var clearRect = new Rectangle(mx, my + InspectorDrawer.RowHeight, ContextMenuWidth, InspectorDrawer.RowHeight);
            var mp = Input.MousePosition;
            if (copyRect.Contains(mp) && _contextMenuLineToCopy != null)
            {
                CopyLineToClipboard(_contextMenuLineToCopy);
                _contextMenuOpen = false;
            }
            else if (clearRect.Contains(mp))
            {
                Clear(clearEngineLogs: true);
                _contextMenuOpen = false;
            }
            else if (!menuRect.Contains(mp))
                _contextMenuOpen = false;
        }
        if (_contextMenuOpen && Input.MouseRightPressed)
            _contextMenuOpen = false;

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
        int levelChipW = 36 + ToggleChipPaddingH * 2;

        x -= ToggleChipPaddingH + 44;
        var eventsRect = new Rectangle(x, toggleY, 44 + ToggleChipPaddingH * 2, HeaderToggleHeight);
        bool eventsOn = ShowEvents;
        Color eventsBg = eventsOn ? new Color(65, 115, 165) : new Color(55, 58, 65);
        spriteBatch.Draw(pixel, eventsRect, eventsBg);
        int eventsTextW = eventsRect.Width - ToggleChipPaddingH * 2;
        int eventsTextH = HeaderToggleHeight - ToggleChipPaddingV * 2;
        InspectorDrawer.DrawLabelScaledToFit(spriteBatch, device, x + ToggleChipPaddingH, toggleY + ToggleChipPaddingV, eventsTextW, eventsTextH, "Events", pixel, eventsOn ? new Color(255, 255, 255) : new Color(160, 165, 175));

        x -= ToggleChipPaddingH + 52 + 4;
        var engineRect = new Rectangle(x, toggleY, 52 + ToggleChipPaddingH * 2, HeaderToggleHeight);
        bool engineOn = ShowEngine;
        Color engineBg = engineOn ? new Color(70, 130, 100) : new Color(55, 58, 65);
        spriteBatch.Draw(pixel, engineRect, engineBg);
        int engineTextW = engineRect.Width - ToggleChipPaddingH * 2;
        int engineTextH = HeaderToggleHeight - ToggleChipPaddingV * 2;
        InspectorDrawer.DrawLabelScaledToFit(spriteBatch, device, x + ToggleChipPaddingH, toggleY + ToggleChipPaddingV, engineTextW, engineTextH, "Engine", pixel, engineOn ? new Color(255, 255, 255) : new Color(160, 165, 175));

        x -= levelChipW + 4;
        var levelRect = new Rectangle(x, toggleY, levelChipW, HeaderToggleHeight);
        string levelLabel = LevelFilterLabel(EngineMinLogLevel);
        Color levelBg = new Color(55, 60, 68);
        spriteBatch.Draw(pixel, levelRect, levelBg);
        InspectorDrawer.DrawLabel(spriteBatch, device, x + ToggleChipPaddingH, toggleY + ToggleChipPaddingV, levelLabel, pixel, new Color(190, 195, 200));
        // Dropdown arrow on level chip
        int ax = levelRect.Right - 8;
        int ay = levelRect.Y + levelRect.Height / 2;
        for (int i = -3; i <= 3; i++)
            for (int j = 0; j <= 4 - Math.Abs(i); j++)
                spriteBatch.Draw(pixel, new Rectangle(ax + i, ay - 2 + j, 1, 1), InspectorDrawer.FoldoutArrow);
        if (_levelDropdownOpen)
        {
            int dropdownY = levelRect.Bottom + 2;
            int dropdownW = levelRect.Width;
            int cursorY = dropdownY;
            InspectorDrawer.DrawDropdownList(spriteBatch, pixel, device, levelRect.X, dropdownY, dropdownW, LevelFilterOptionLabels, (int)EngineMinLogLevel, ref cursorY, Input?.MousePosition);
        }
    }

    protected override void DrawContent(SpriteBatch spriteBatch)
    {
        var content = ContentBounds;
        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);
        var device = spriteBatch.GraphicsDevice;
        int contentWidth = Math.Max(0, content.Width - ScrollbarWidth);
        int logAreaTop = content.Y + ShortcutsLineHeight;
        var logArea = new Rectangle(content.X, logAreaTop, contentWidth, content.Height - ShortcutsLineHeight);

        // Shortcuts line at top of content (always visible)
        InspectorDrawer.DrawLabel(spriteBatch, device, content.X + Padding, content.Y + 2, ShortcutsText, pixel, new Color(100, 105, 112));

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

        int y = logAreaTop - _scrollY;
        for (int i = 0; i < _displayLines.Count; i++)
        {
            int lineBottom = y + LineHeight;
            if (lineBottom > logAreaTop && y < content.Bottom)
            {
                if (i == selectedIndex)
                {
                    var highlightRect = new Rectangle(content.X, y, logAreaWidth, LineHeight);
                    spriteBatch.Draw(pixel, highlightRect, new Color(55, 62, 75));
                }
                var line = _displayLines[i];
                InspectorDrawer.DrawLabelScrollable(spriteBatch, device, content.X + Padding, y + 2, logAreaWidth, _scrollX, line.Text, pixel, line.Color);
            }
            y = lineBottom;
        }

        int logVisibleHeight = content.Height - ShortcutsLineHeight;
        var logContentRect = new Rectangle(content.X, logAreaTop, content.Width, logVisibleHeight);
        if (_contentHeight > logVisibleHeight && content.Width > ScrollbarWidth)
        {
            var scrollbar = GetScrollbarBounds(logContentRect);
            ScrollbarRoundedDrawer.DrawRoundedScrollbar(spriteBatch, device, scrollbar, new Color(42, 45, 50));
            var thumb = GetThumbBounds(logContentRect, Math.Max(0, _contentHeight - logVisibleHeight));
            ScrollbarRoundedDrawer.DrawRoundedScrollbar(spriteBatch, device, thumb, new Color(65, 70, 78));
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

        if (_contextMenuOpen)
        {
            int menuH = 2 * InspectorDrawer.RowHeight;
            int mx = _contextMenuX;
            int my = _contextMenuY;
            if (mx + ContextMenuWidth > Bounds.Right) mx = Bounds.Right - ContextMenuWidth;
            if (my + menuH > Bounds.Bottom) my = Bounds.Bottom - menuH;
            if (mx < Bounds.X) mx = Bounds.X;
            if (my < Bounds.Y) my = Bounds.Y;
            var menuRect = new Rectangle(mx, my, ContextMenuWidth, menuH);
            spriteBatch.Draw(pixel, menuRect, InspectorDrawer.SectionBg);
            spriteBatch.Draw(pixel, new Rectangle(menuRect.X - 1, menuRect.Y - 1, menuRect.Width + 2, menuRect.Height + 2), InspectorDrawer.ControlBorder);
            var copyRect = new Rectangle(mx, my, ContextMenuWidth, InspectorDrawer.RowHeight);
            var clearRect = new Rectangle(mx, my + InspectorDrawer.RowHeight, ContextMenuWidth, InspectorDrawer.RowHeight);
            var mp = Input?.MousePosition ?? Point.Zero;
            if (copyRect.Contains(mp))
                spriteBatch.Draw(pixel, copyRect, InspectorDrawer.DropdownHoverBg);
            else
                spriteBatch.Draw(pixel, copyRect, _contextMenuLineToCopy != null ? InspectorDrawer.RowBg : new Color(38, 40, 45));
            if (clearRect.Contains(mp))
                spriteBatch.Draw(pixel, clearRect, InspectorDrawer.DropdownHoverBg);
            else
                spriteBatch.Draw(pixel, clearRect, InspectorDrawer.RowBg);
            InspectorDrawer.DrawLabel(spriteBatch, device, mx + InspectorDrawer.Padding, my + 2, "Copy", pixel, _contextMenuLineToCopy != null ? InspectorDrawer.TextColor : new Color(100, 105, 112));
            InspectorDrawer.DrawLabel(spriteBatch, device, mx + InspectorDrawer.Padding, my + InspectorDrawer.RowHeight + 2, "Clear", pixel, InspectorDrawer.TextColor);
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
