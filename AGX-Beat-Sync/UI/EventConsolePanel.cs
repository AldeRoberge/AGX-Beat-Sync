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
/// Console panel that displays all events triggered during playback (e.g. Spawn Entity at timeline times).
/// </summary>
public class EventConsolePanel : PanelBase
{
    private const int LineHeight = 18;
    private const int Padding = 6;
    private const int ScrollbarWidth = 10;
    private const int MinScrollbarThumbHeight = 20;
    private const int MaxLogEntries = 500;

    private readonly List<string> _entries = new();
    private readonly object _entriesLock = new();
    private int _scrollY;
    private int _contentHeight;
    private bool _scrollbarThumbDragging;
    private int _thumbDragStartY;
    private int _scrollStartY;
    /// <summary>Selected line index (0-based), or -1 if none.</summary>
    private int _selectedIndex = -1;
    /// <summary>True when scroll position is at bottom; used to auto-scroll only if user hasn't scrolled up.</summary>
    private volatile bool _userAtBottom = true;

    public EventConsolePanel()
    {
        Title = "Event Console";
        BackgroundColor = new Color(32, 34, 38);
        HeaderColor = new Color(45, 48, 54);
    }

    /// <summary>Log an event fired by the system (thread-safe).</summary>
    public void LogEvent(double timeSeconds, string trackName, string message)
    {
        string timeStr = TimeFormatHelper.Format(timeSeconds);
        string line = $"{timeStr} | {trackName} | {message}";
        lock (_entriesLock)
        {
            _entries.Add(line);
            if (_entries.Count > MaxLogEntries)
            {
                _entries.RemoveAt(0);
                if (_selectedIndex > 0) _selectedIndex--;
                else if (_selectedIndex == 0) _selectedIndex = -1;
            }
        }
        if (_userAtBottom)
            _scrollY = int.MaxValue;
    }

    /// <summary>Clear all log entries (e.g. when starting a new playback).</summary>
    public void Clear()
    {
        lock (_entriesLock)
            _entries.Clear();
        _scrollY = 0;
        _selectedIndex = -1;
        _userAtBottom = true;
    }

    /// <summary>Copy the selected log entry to the clipboard. Returns true if something was copied.</summary>
    public bool CopySelectionToClipboard()
    {
        lock (_entriesLock)
        {
            if (_selectedIndex < 0 || _selectedIndex >= _entries.Count) return false;
            string toCopy = _entries[_selectedIndex];
            if (string.IsNullOrEmpty(toCopy)) return false;
            try
            {
                Clipboard.SetText(toCopy);
                return true;
            }
            catch { return false; }
        }
    }

    /// <summary>Remove the selected log entry (for Cut). Returns true if an entry was removed.</summary>
    public bool RemoveSelectedEntry()
    {
        lock (_entriesLock)
        {
            if (_selectedIndex < 0 || _selectedIndex >= _entries.Count) return false;
            _entries.RemoveAt(_selectedIndex);
            if (_selectedIndex >= _entries.Count) _selectedIndex = _entries.Count - 1;
            if (_entries.Count == 0) _selectedIndex = -1;
            return true;
        }
    }

    public InputManager? Input { get; set; }

    public override void Update(GameTime gameTime)
    {
        if (Input == null) return;

        var content = ContentBounds;
        int contentWidth = Math.Max(0, content.Width - ScrollbarWidth);
        int visibleHeight = content.Height;
        lock (_entriesLock)
            _contentHeight = _entries.Count * LineHeight;

        int maxScroll = Math.Max(0, _contentHeight - visibleHeight);
        _scrollY = Math.Clamp(_scrollY, 0, maxScroll);

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
                var thumb = GetThumbBounds(content, maxScroll);
                var scrollbar = GetScrollbarBounds(content);
                if (scrollbar.Contains(Input.MousePosition))
                {
                    _scrollbarThumbDragging = true;
                    _thumbDragStartY = Input.MousePosition.Y;
                    _scrollStartY = _scrollY;
                }
                else
                {
                    var logArea = new Rectangle(content.X, content.Y, Math.Max(0, content.Width - ScrollbarWidth), content.Height);
                    if (logArea.Contains(Input.MousePosition))
                    {
                        int lineIndex = (Input.MousePosition.Y - content.Y + _scrollY) / LineHeight;
                        lock (_entriesLock)
                        {
                            if (lineIndex >= 0 && lineIndex < _entries.Count)
                                _selectedIndex = lineIndex;
                            else
                                _selectedIndex = -1;
                        }
                    }
                }
            }

            bool ctrl = Input.IsKeyDown(Keys.LeftControl) || Input.IsKeyDown(Keys.RightControl);
            if (ContainsPoint(Input.MousePosition) && ctrl && Input.IsKeyPressed(Keys.C))
            {
                string? toCopy = null;
                lock (_entriesLock)
                {
                    if (_selectedIndex >= 0 && _selectedIndex < _entries.Count)
                        toCopy = _entries[_selectedIndex];
                }
                if (!string.IsNullOrEmpty(toCopy))
                {
                    try { Clipboard.SetText(toCopy); } catch { /* ignore clipboard errors */ }
                }
            }
        }

        _userAtBottom = maxScroll <= 0 || _scrollY >= maxScroll;
    }

    protected override void DrawContent(SpriteBatch spriteBatch)
    {
        var content = ContentBounds;
        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);
        var device = spriteBatch.GraphicsDevice;

        int contentWidth = Math.Max(0, content.Width - ScrollbarWidth);
        var logArea = new Rectangle(content.X, content.Y, contentWidth, content.Height);

        List<string> snapshot;
        lock (_entriesLock)
            snapshot = new List<string>(_entries);

        if (snapshot.Count == 0)
        {
            InspectorDrawer.DrawLabel(spriteBatch, device, logArea.X + Padding, logArea.Y + Padding, "No events yet. Press Play to trigger timeline events.", pixel, new Color(140, 145, 150));
            return;
        }

        int logAreaWidth = Math.Max(0, content.Width - ScrollbarWidth);
        int selectedIndex = _selectedIndex;

        // Draw only visible lines (no End/Begin/scissor so we don't break the batch and corrupt the inspector)
        int y = content.Y - _scrollY;
        for (int i = 0; i < snapshot.Count; i++)
        {
            int lineBottom = y + LineHeight;
            if (lineBottom > content.Y && y < content.Bottom)
            {
                if (i == selectedIndex)
                {
                    var highlightRect = new Rectangle(content.X, y, logAreaWidth, LineHeight);
                    spriteBatch.Draw(pixel, highlightRect, new Color(60, 70, 90));
                }
                InspectorDrawer.DrawLabel(spriteBatch, device, content.X + Padding, y + 2, snapshot[i], pixel);
            }
            y = lineBottom;
        }

        // Scrollbar
        if (_contentHeight > content.Height && content.Width > ScrollbarWidth)
        {
            var scrollbar = GetScrollbarBounds(content);
            spriteBatch.Draw(pixel, scrollbar, new Color(50, 52, 58));
            var thumb = GetThumbBounds(content, Math.Max(0, _contentHeight - content.Height));
            spriteBatch.Draw(pixel, thumb, new Color(70, 74, 82));
            // Gizmo: center grip to show thumb is grabbable (like in/out region)
            if (thumb.Height >= 10)
            {
                var gripColor = new Color(45, 50, 58);
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
