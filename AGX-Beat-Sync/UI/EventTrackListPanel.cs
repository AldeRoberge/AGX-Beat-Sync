using System.Linq;
using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Editor;
using AGX_Beat_Sync.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.UI;

/// <summary>
/// Left panel: vertical list of event tracks. Click to select; Ctrl+click to toggle; Shift+click to range-select; drag to reorder; remove via button or Delete key. Create new track at bottom.
/// </summary>
public class EventTrackListPanel : PanelBase
{
    /// <summary>Match TimelinePanel.LaneHeight (28) so track rows align with piano roll lanes.</summary>
    private const int TrackRowHeight = 28;
    private const int AddButtonWidth = 100;
    private const int AddButtonHeight = 24;
    private const int AddButtonMargin = 2;
    private const int RemoveButtonWidth = 22;
    private const int Padding = 8;
    /// <summary>Right padding so the delete buttons don't sit flush against the piano roll. Filled with panel background.</summary>
    private const int RightPaddingPx = 8;
    private const int DragThresholdPx = 4;
    /// <summary>Grab handle: two horizontal lines on the left of each track row.</summary>
    private const int GrabHandleLeft = 4;
    private const int GrabHandleLineWidth = 8;
    private const int GrabHandleLineHeight = 1;
    private const int GrabHandleLineGap = 2;
    private static readonly Color GrabHandleColor = new(110, 115, 125);

    private const int ScrollbarWidth = 10;
    private const int MinScrollbarThumbHeight = 20;

    /// <summary>Content Y aligned with timeline track area (HeaderHeight + in/out + playhead strips = 48).</summary>
    private const int TrackListContentTopOffset = 48;

    public override Rectangle ContentBounds =>
        new(Bounds.X, Bounds.Y + TrackListContentTopOffset, Bounds.Width - RightPaddingPx, Math.Max(0, Bounds.Height - TrackListContentTopOffset));

    private int _scrollY;
    private bool _scrollbarThumbDragging;
    private int _scrollStartY;
    private int _scrollThumbDragStartY;

    private int? _potentialDragIndex;
    private int? _draggingTrackIndex;
    private int? _dropTargetIndex;
    private Point _dragStartPos;

    /// <summary>Track type for the next added track: Spawn Entity when no tracks, otherwise the type of the first (top) track.</summary>
    private static string GetTypeIdForNewTrack(Project project)
    {
        if (project.EventTracks.Count == 0)
            return "SpawnEntity";
        return project.EventTracks[0].TrackTypeId;
    }

    public Project? Project { get; set; }
    public EditorSelection? Selection { get; set; }
    public InputManager? Input { get; set; }
    /// <summary>When true, show 1–9 and 0 next to tracks to indicate record shortcut keys.</summary>
    public bool RecordMode { get; set; }
    /// <summary>When user clicks the remove track button; game should show confirmation then remove if confirmed.</summary>
    public Action<EventTrackBase>? OnDeleteTrackRequested { get; set; }

    public EventTrackListPanel()
    {
        Title = "Tracks";
        BackgroundColor = new Color(32, 34, 38);
    }

    private Rectangle GetListArea()
    {
        var content = ContentBounds;
        int contentHeight = GetContentHeight();
        if (contentHeight <= content.Height)
            return content;
        return new Rectangle(content.X, content.Y, Math.Max(0, content.Width - ScrollbarWidth), content.Height);
    }

    private int GetContentHeight()
    {
        if (Project == null) return TrackRowHeight;
        return Project.EventTracks.Count * TrackRowHeight + TrackRowHeight; // tracks + add row
    }

    private int GetAddRowY()
    {
        if (Project == null) return ContentBounds.Y;
        return ContentBounds.Y + Project.EventTracks.Count * TrackRowHeight - _scrollY;
    }

    private static Rectangle GetScrollbarBounds(Rectangle content) =>
        new(content.Right - ScrollbarWidth, content.Y, ScrollbarWidth, content.Height);

    private Rectangle GetThumbBounds(Rectangle content, int maxScroll)
    {
        var scrollbar = GetScrollbarBounds(content);
        int _contentHeight = GetContentHeight();
        if (_contentHeight <= 0 || _contentHeight <= content.Height)
            return new Rectangle(scrollbar.X, scrollbar.Y, scrollbar.Width, Math.Min(scrollbar.Height, MinScrollbarThumbHeight));
        int thumbHeight = Math.Max(MinScrollbarThumbHeight, (int)(content.Height / (double)_contentHeight * scrollbar.Height));
        int travel = scrollbar.Height - thumbHeight;
        int thumbY = travel > 0 && maxScroll > 0
            ? scrollbar.Y + (int)(_scrollY / (double)maxScroll * travel)
            : scrollbar.Y;
        return new Rectangle(scrollbar.X, thumbY, scrollbar.Width, thumbHeight);
    }

    public override string? GetHoverText(Point mouse)
    {
        if (Project == null || !ContentBounds.Contains(mouse)) return null;
        var listArea = GetListArea();
        if (listArea.Contains(mouse))
        {
            int virtualY = mouse.Y - listArea.Y + _scrollY;
            int rowIndex = virtualY / TrackRowHeight;
            var tracks = Project.EventTracks;
            if (rowIndex >= 0 && rowIndex < tracks.Count)
            {
                var track = tracks[rowIndex];
                int trackScreenY = listArea.Y + rowIndex * TrackRowHeight - _scrollY;
                var rowRect = new Rectangle(listArea.X, trackScreenY, listArea.Width, TrackRowHeight);
                var removeRect = new Rectangle(rowRect.Right - RemoveButtonWidth - 2, trackScreenY + 2, RemoveButtonWidth, TrackRowHeight - 4);
                if (removeRect.Contains(mouse)) return $"Remove track: {track.DisplayName}";
                return $"Track: {track.DisplayName} (drag to reorder)";
            }
            var addRowY = GetAddRowY();
            var addRect = new Rectangle(listArea.X + Padding, addRowY + AddButtonMargin, AddButtonWidth, AddButtonHeight);
            if (addRect.Contains(mouse)) return "Add track";
        }
        if (GetContentHeight() > listArea.Height && GetScrollbarBounds(ContentBounds).Contains(mouse))
            return "Scroll tracks";
        return "Tracks";
    }

    public override void Update(GameTime gameTime)
    {
        if (Project == null || Selection == null || Input == null)
            return;

        var content = ContentBounds;
        var listArea = GetListArea();
        var tracks = Project.EventTracks;
        int _contentHeight = GetContentHeight();
        int maxScroll = Math.Max(0, _contentHeight - listArea.Height);

        // Clamp scroll to valid range
        _scrollY = Math.Clamp(_scrollY, 0, maxScroll);

        int addRowY = GetAddRowY();

        // Scrollbar thumb drag
        if (_scrollbarThumbDragging)
        {
            if (Input.MouseLeftDown)
            {
                var scrollbar = GetScrollbarBounds(content);
                var thumb = GetThumbBounds(content, maxScroll);
                int travel = scrollbar.Height - thumb.Height;
                if (travel > 0 && maxScroll > 0)
                {
                    int deltaY = Input.MousePosition.Y - _scrollThumbDragStartY;
                    int scrollDelta = (int)(deltaY / (double)travel * maxScroll);
                    _scrollY = Math.Clamp(_scrollStartY + scrollDelta, 0, maxScroll);
                }
            }
            else
                _scrollbarThumbDragging = false;
        }

        // Mouse wheel scroll over list area
        if (Input.ScrollWheelDelta != 0 && listArea.Contains(Input.MousePosition))
        {
            _scrollY -= Input.ScrollWheelDelta;
            _scrollY = Math.Clamp(_scrollY, 0, maxScroll);
        }

        // Mouse released: commit drag reorder
        if (Input.MouseLeftReleased)
        {
            if (_draggingTrackIndex.HasValue && _dropTargetIndex.HasValue && _draggingTrackIndex.Value != _dropTargetIndex.Value)
            {
                int from = _draggingTrackIndex.Value;
                int to = _dropTargetIndex.Value;
                if (from >= 0 && from < tracks.Count && to >= 0 && to < tracks.Count)
                {
                    var track = tracks[from];
                    tracks.RemoveAt(from);
                    int insertAt = to > from ? to - 1 : to;
                    tracks.Insert(insertAt, track);
                    for (int i = 0; i < tracks.Count; i++)
                        tracks[i].Order = i;
                }
            }
            _potentialDragIndex = null;
            _draggingTrackIndex = null;
            _dropTargetIndex = null;
        }

        // Update drop target while dragging (use virtual Y)
        if (_draggingTrackIndex.HasValue && content.Contains(Input.MousePosition))
        {
            int virtualY = Input.MousePosition.Y - listArea.Y + _scrollY;
            int idx = virtualY / TrackRowHeight;
            if (idx < 0) idx = 0;
            if (idx >= tracks.Count) idx = tracks.Count - 1;
            _dropTargetIndex = idx;
        }

        // Start drag when mouse moves past threshold
        if (_potentialDragIndex.HasValue && Input.MouseLeftDown)
        {
            int dx = Input.MousePosition.X - _dragStartPos.X;
            int dy = Input.MousePosition.Y - _dragStartPos.Y;
            if (dx * dx + dy * dy >= DragThresholdPx * DragThresholdPx)
            {
                _draggingTrackIndex = _potentialDragIndex.Value;
                _potentialDragIndex = null;
            }
        }

        if (!Input.MouseLeftPressed)
            return;

        // Scrollbar click (jump or start thumb drag)
        if (_contentHeight > listArea.Height && content.Width > ScrollbarWidth)
        {
                var scrollbar = GetScrollbarBounds(content);
                if (scrollbar.Contains(Input.MousePosition))
                {
                    var thumb = GetThumbBounds(content, maxScroll);
                    if (thumb.Contains(Input.MousePosition))
                    {
                        _scrollbarThumbDragging = true;
                        _scrollThumbDragStartY = Input.MousePosition.Y;
                        _scrollStartY = _scrollY;
                    }
                    else
                {
                    int jumpY = Input.MousePosition.Y - scrollbar.Y - thumb.Height / 2;
                    int travel = scrollbar.Height - thumb.Height;
                    if (travel > 0 && maxScroll > 0)
                        _scrollY = Math.Clamp((int)(jumpY / (double)travel * maxScroll), 0, maxScroll);
                }
                return;
            }
        }

        if (!listArea.Contains(Input.MousePosition))
            return;

        int virtualMouseY = Input.MousePosition.Y - listArea.Y + _scrollY;

        // Add row: Add button (at bottom)
        var addRect = new Rectangle(listArea.X + Padding, addRowY + AddButtonMargin, AddButtonWidth, AddButtonHeight);
        if (addRect.Contains(Input.MousePosition))
        {
            string typeId = GetTypeIdForNewTrack(Project);
            var track = EventTrackRegistry.CreateTrack(typeId);
            track.TrackColor = EventTrackBase.GetRandomTrackColor();
            track.Order = tracks.Count;
            tracks.Add(track);
            Selection.SelectedEventTrack = track;
            Selection.SelectedEventTime = null;
            return;
        }

        // Track rows (use virtual Y to get row index)
        int rowIndex = virtualMouseY / TrackRowHeight;
        if (rowIndex >= 0 && rowIndex < tracks.Count)
        {
            var track = tracks[rowIndex];
            int trackScreenY = listArea.Y + rowIndex * TrackRowHeight - _scrollY;
            var rowRect = new Rectangle(listArea.X, trackScreenY, listArea.Width, TrackRowHeight);
            var removeRect = new Rectangle(rowRect.Right - RemoveButtonWidth - 2, trackScreenY + 2, RemoveButtonWidth, TrackRowHeight - 4);

            if (removeRect.Contains(Input.MousePosition))
            {
                OnDeleteTrackRequested?.Invoke(track);
                return;
            }

            if (rowRect.Contains(Input.MousePosition) && !removeRect.Contains(Input.MousePosition))
            {
                if (_draggingTrackIndex.HasValue)
                    return;
                bool ctrl = Input.IsKeyDown(Keys.LeftControl) || Input.IsKeyDown(Keys.RightControl);
                bool shift = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
                if (ctrl)
                {
                    Selection.ToggleTrackSelection(track);
                    Selection.SelectedEventTime = null;
                }
                else if (shift)
                {
                    int primaryIndex = Selection.SelectedEventTrack != null ? tracks.FindIndex(t => ReferenceEquals(t, Selection.SelectedEventTrack)) : -1;
                    int from = primaryIndex >= 0 ? primaryIndex : rowIndex;
                    Selection.SelectTrackRange(from, rowIndex, tracks.Cast<IEventTrack>().ToList());
                    Selection.SelectedEventTime = null;
                }
                else
                {
                    Selection.SelectedEventTrack = track;
                    Selection.SelectedEventTime = null;
                }
                if (!_potentialDragIndex.HasValue)
                {
                    _potentialDragIndex = rowIndex;
                    _dragStartPos = Input.MousePosition;
                }
                return;
            }
        }
    }

    protected override void DrawContent(SpriteBatch spriteBatch)
    {
        if (Project == null)
            return;

        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);
        var content = ContentBounds;
        var listArea = GetListArea();
        var device = spriteBatch.GraphicsDevice;
        var tracks = Project.EventTracks;

        int firstVisible = (int)Math.Floor(_scrollY / (double)TrackRowHeight);
        int lastVisible = (int)Math.Floor((_scrollY + listArea.Height - 1) / (double)TrackRowHeight);

        for (int index = firstVisible; index <= lastVisible && index < tracks.Count; index++)
        {
            var track = tracks[index];
            bool isDragging = _draggingTrackIndex == index;
            int y = listArea.Y + index * TrackRowHeight - _scrollY;

            if (!isDragging)
            {
                bool selected = Selection != null && Selection.IsTrackSelected(track);
                var rowRect = new Rectangle(listArea.X, y, listArea.Width, TrackRowHeight);
                var rowBg = selected ? new Color(62, 68, 78) : (index % 2 == 0 ? new Color(38, 40, 44) : new Color(35, 37, 41));
                spriteBatch.Draw(pixel, rowRect, rowBg);

                // Grab handle (two horizontal lines) to show row is draggable
                int line1Y = y + (TrackRowHeight - GrabHandleLineGap - GrabHandleLineHeight * 2) / 2;
                int line2Y = line1Y + GrabHandleLineHeight + GrabHandleLineGap;
                spriteBatch.Draw(pixel, new Rectangle(listArea.X + GrabHandleLeft, line1Y, GrabHandleLineWidth, GrabHandleLineHeight), GrabHandleColor);
                spriteBatch.Draw(pixel, new Rectangle(listArea.X + GrabHandleLeft, line2Y, GrabHandleLineWidth, GrabHandleLineHeight), GrabHandleColor);

                InspectorDrawer.DrawLabel(spriteBatch, device, listArea.X + GrabHandleLeft + GrabHandleLineWidth + 4, y + 4, track.DisplayName.Length > 16 ? track.DisplayName[..16] + "…" : track.DisplayName, pixel);

                // Record mode: show key hint (1–9, 0) for first 10 tracks
                if (RecordMode && index < 10)
                {
                    string keyHint = (index + 1) % 10 == 0 ? "0" : ((index + 1) % 10).ToString();
                    int keyX = rowRect.Right - RemoveButtonWidth - 20;
                    InspectorDrawer.DrawLabel(spriteBatch, device, keyX, y + 4, keyHint, pixel, new Color(140, 180, 220));
                }

                var removeRect = new Rectangle(rowRect.Right - RemoveButtonWidth - 2, y + 2, RemoveButtonWidth, TrackRowHeight - 4);
                InspectorDrawer.DrawLabel(spriteBatch, device, removeRect.X + 6, removeRect.Y + 2, "×", pixel);
            }
        }

        // Drop indicator on top so it's visible while dragging
        if (_draggingTrackIndex.HasValue && _dropTargetIndex.HasValue)
        {
            int indicatorY = listArea.Y + _dropTargetIndex.Value * TrackRowHeight - _scrollY;
            var indicatorRect = new Rectangle(listArea.X, indicatorY, listArea.Width, 2);
            spriteBatch.Draw(pixel, indicatorRect, new Color(100, 150, 220));
        }

        // Create new track row at bottom (only if visible)
        int addRowY = GetAddRowY();
        if (addRowY + TrackRowHeight > listArea.Y && addRowY < listArea.Bottom)
        {
            spriteBatch.Draw(pixel, new Rectangle(listArea.X, addRowY, listArea.Width, TrackRowHeight), new Color(45, 48, 54));
            var addRect = new Rectangle(listArea.X + Padding, addRowY + AddButtonMargin, AddButtonWidth, AddButtonHeight);
            // Button background with subtle border
            spriteBatch.Draw(pixel, new Rectangle(addRect.X - 1, addRect.Y - 1, addRect.Width + 2, addRect.Height + 2), new Color(55, 65, 80));
            spriteBatch.Draw(pixel, addRect, new Color(72, 105, 140));
            var (plusW, plusH) = InspectorDrawer.MeasureLabel("+");
            int plusX = addRect.X + (addRect.Width - plusW) / 2;
            int plusY = addRect.Y + (addRect.Height - plusH) / 2;
            InspectorDrawer.DrawLabel(spriteBatch, device, plusX, plusY, "+", pixel, Color.White);
        }

        // Scrollbar when content overflows
        int _contentHeight = GetContentHeight();
        if (_contentHeight > listArea.Height && content.Width > ScrollbarWidth)
        {
            var scrollbar = GetScrollbarBounds(content);
            ScrollbarRoundedDrawer.DrawRoundedScrollbarTrack(spriteBatch, device, scrollbar, new Color(50, 52, 58), pixel);
            int maxScroll = Math.Max(0, _contentHeight - listArea.Height);
            var thumb = GetThumbBounds(content, maxScroll);
            ScrollbarRoundedDrawer.DrawRoundedScrollbar(spriteBatch, device, thumb, new Color(80, 84, 92));
        }
    }
}
