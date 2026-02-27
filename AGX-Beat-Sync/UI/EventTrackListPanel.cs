using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Editor;
using AGX_Beat_Sync.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.UI;

/// <summary>
/// Left panel: vertical list of event tracks. Click to select; drag to reorder; remove via button or Delete key. Create new track at bottom.
/// </summary>
public class EventTrackListPanel : PanelBase
{
    private const int TrackRowHeight = 26;
    private const int DropdownWidth = 130;
    private const int AddButtonWidth = 36;
    private const int RemoveButtonWidth = 22;
    private const int Padding = 8;
    /// <summary>Right padding so the delete buttons don't sit flush against the piano roll. Filled with panel background.</summary>
    private const int RightPaddingPx = 8;
    private const int DragThresholdPx = 4;

    public override Rectangle ContentBounds => new(Bounds.X, Bounds.Y + HeaderHeight, Bounds.Width - RightPaddingPx, Bounds.Height - HeaderHeight);

    private bool _addDropdownOpen;
    private string _selectedTypeIdForAdd = "";

    private int? _potentialDragIndex;
    private int? _draggingTrackIndex;
    private int? _dropTargetIndex;
    private Point _dragStartPos;

    public Project? Project { get; set; }
    public EditorSelection? Selection { get; set; }
    public InputManager? Input { get; set; }

    public EventTrackListPanel()
    {
        Title = "Tracks";
        BackgroundColor = new Color(32, 34, 38);
    }

    private int GetAddRowY()
    {
        if (Project == null) return ContentBounds.Y;
        return ContentBounds.Y + Project.EventTracks.Count * TrackRowHeight;
    }

    public override string? GetHoverText(Point mouse)
    {
        if (Project == null || !ContentBounds.Contains(mouse)) return null;
        var content = ContentBounds;
        int y = content.Y;
        foreach (var track in Project.EventTracks)
        {
            var rowRect = new Rectangle(content.X, y, content.Width, TrackRowHeight);
            if (rowRect.Contains(mouse))
            {
                var removeRect = new Rectangle(rowRect.Right - RemoveButtonWidth - 2, y + 2, RemoveButtonWidth, TrackRowHeight - 4);
                if (removeRect.Contains(mouse)) return $"Remove track: {track.DisplayName}";
                return $"Track: {track.DisplayName} (drag to reorder)";
            }
            y += TrackRowHeight;
        }
        var addRowY = GetAddRowY();
        var dropdownRect = new Rectangle(content.X + Padding, addRowY + 4, DropdownWidth, TrackRowHeight - 8);
        if (dropdownRect.Contains(mouse)) return "Track type for new track";
        var addRect = new Rectangle(content.X + Padding + DropdownWidth + 4, addRowY + 4, AddButtonWidth, TrackRowHeight - 8);
        if (addRect.Contains(mouse)) return "Add track";
        return "Tracks";
    }

    public override void Update(GameTime gameTime)
    {
        if (Project == null || Selection == null || Input == null)
            return;

        var content = ContentBounds;
        var tracks = Project.EventTracks;

        if (string.IsNullOrEmpty(_selectedTypeIdForAdd) && EventTrackRegistry.AllTypes.Count > 0)
            _selectedTypeIdForAdd = EventTrackRegistry.AllTypes[0].TrackTypeId;

        int addRowY = GetAddRowY();

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

        // Update drop target while dragging
        if (_draggingTrackIndex.HasValue && content.Contains(Input.MousePosition))
        {
            int mouseY = Input.MousePosition.Y - content.Y;
            int idx = mouseY / TrackRowHeight;
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

        if (!content.Contains(Input.MousePosition))
        {
            _addDropdownOpen = false;
            return;
        }

        // Add row: dropdown + Add button (at bottom)
        var dropdownRect = new Rectangle(content.X + Padding, addRowY + 4, DropdownWidth, TrackRowHeight - 8);
        var addRect = new Rectangle(content.X + Padding + DropdownWidth + 4, addRowY + 4, AddButtonWidth, TrackRowHeight - 8);
        if (dropdownRect.Contains(Input.MousePosition))
        {
            _addDropdownOpen = !_addDropdownOpen;
            return;
        }
        if (addRect.Contains(Input.MousePosition))
        {
            _addDropdownOpen = false;
            if (!string.IsNullOrEmpty(_selectedTypeIdForAdd))
            {
                var track = EventTrackRegistry.CreateTrack(_selectedTypeIdForAdd);
                track.TrackColor = EventTrackBase.GetRandomTrackColor();
                track.Order = tracks.Count;
                tracks.Add(track);
                Selection.SelectedEventTrack = track;
            }
            return;
        }

        // Add dropdown list (below or above add row depending on space)
        if (_addDropdownOpen && EventTrackRegistry.AllTypes.Count > 0)
        {
            int dropdownHeight = 4 + EventTrackRegistry.AllTypes.Count * TrackRowHeight;
            bool openUp = addRowY + TrackRowHeight + dropdownHeight > content.Bottom;
            int listY = openUp ? addRowY - dropdownHeight : addRowY + TrackRowHeight + 4;
            foreach (var desc in EventTrackRegistry.AllTypes)
            {
                var rowRect = new Rectangle(content.X + Padding, listY, content.Width - Padding * 2, TrackRowHeight);
                if (rowRect.Contains(Input.MousePosition))
                {
                    _selectedTypeIdForAdd = desc.TrackTypeId;
                    _addDropdownOpen = false;
                    return;
                }
                listY += TrackRowHeight;
            }
        }

        // Track rows
        int trackY = content.Y;
        for (int i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            var rowRect = new Rectangle(content.X, trackY, content.Width, TrackRowHeight);
            var removeRect = new Rectangle(rowRect.Right - RemoveButtonWidth - 2, trackY + 2, RemoveButtonWidth, TrackRowHeight - 4);

            if (removeRect.Contains(Input.MousePosition))
            {
                tracks.Remove(track);
                if (Selection.SelectedEventTrack == track)
                    Selection.SelectedEventTrack = tracks.FirstOrDefault();
                _addDropdownOpen = false;
                return;
            }

            if (rowRect.Contains(Input.MousePosition) && !removeRect.Contains(Input.MousePosition))
            {
                if (_draggingTrackIndex.HasValue)
                    return;
                if (!_potentialDragIndex.HasValue)
                {
                    _potentialDragIndex = i;
                    _dragStartPos = Input.MousePosition;
                }
                Selection.SelectedEventTrack = track;
                _addDropdownOpen = false;
                return;
            }

            trackY += TrackRowHeight;
        }

        // Close dropdown when clicking below/above the list (depending on open direction)
        if (_addDropdownOpen && EventTrackRegistry.AllTypes.Count > 0)
        {
            int dropdownHeight = 4 + EventTrackRegistry.AllTypes.Count * TrackRowHeight;
            bool openUp = addRowY + TrackRowHeight + dropdownHeight > content.Bottom;
            int listTop = openUp ? addRowY - dropdownHeight : addRowY + TrackRowHeight + 4;
            int listBottom = listTop + dropdownHeight;
            if (Input.MousePosition.Y < listTop || Input.MousePosition.Y > listBottom)
                _addDropdownOpen = false;
        }
    }

    protected override void DrawContent(SpriteBatch spriteBatch)
    {
        if (Project == null)
            return;

        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);
        var content = ContentBounds;
        var device = spriteBatch.GraphicsDevice;
        var tracks = Project.EventTracks;
        int y = content.Y;

        for (int index = 0; index < tracks.Count; index++)
        {
            var track = tracks[index];
            bool isDragging = _draggingTrackIndex == index;
            bool isDropTarget = _dropTargetIndex == index;

            if (isDropTarget && _draggingTrackIndex.HasValue)
            {
                var indicatorRect = new Rectangle(content.X, y, content.Width, 2);
                spriteBatch.Draw(pixel, indicatorRect, new Color(100, 150, 220));
            }

            if (!isDragging)
            {
                bool selected = Selection?.SelectedEventTrack == track;
                var rowRect = new Rectangle(content.X, y, content.Width, TrackRowHeight);
                var rowBg = selected ? new Color(62, 68, 78) : (index % 2 == 0 ? new Color(38, 40, 44) : new Color(35, 37, 41));
                spriteBatch.Draw(pixel, rowRect, rowBg);

                InspectorDrawer.DrawLabel(spriteBatch, device, content.X + Padding + 4, y + 4, track.DisplayName.Length > 16 ? track.DisplayName[..16] + "…" : track.DisplayName, pixel);

                var removeRect = new Rectangle(rowRect.Right - RemoveButtonWidth - 2, y + 2, RemoveButtonWidth, TrackRowHeight - 4);
                spriteBatch.Draw(pixel, removeRect, new Color(80, 50, 50));
                InspectorDrawer.DrawLabel(spriteBatch, device, removeRect.X + 6, removeRect.Y + 2, "×", pixel);
            }

            y += TrackRowHeight;
        }

        // Create new track row at bottom
        int addRowY = GetAddRowY();
        spriteBatch.Draw(pixel, new Rectangle(content.X, addRowY, content.Width, TrackRowHeight), new Color(45, 48, 54));
        var dropdownRect = new Rectangle(content.X + Padding, addRowY + 4, DropdownWidth, TrackRowHeight - 8);
        spriteBatch.Draw(pixel, dropdownRect, new Color(58, 62, 70));
        spriteBatch.Draw(pixel, new Rectangle(dropdownRect.X - 1, dropdownRect.Y - 1, dropdownRect.Width + 2, dropdownRect.Height + 2), new Color(70, 74, 82));
        var desc = EventTrackRegistry.AllTypes.FirstOrDefault(d => d.TrackTypeId == _selectedTypeIdForAdd);
        string dropdownText = desc?.DisplayName ?? _selectedTypeIdForAdd;
        InspectorDrawer.DrawLabel(spriteBatch, device, dropdownRect.X + 6, dropdownRect.Y + 4, dropdownText.Length > 14 ? dropdownText[..14] + "…" : dropdownText, pixel);
        var addRect = new Rectangle(content.X + Padding + DropdownWidth + 4, addRowY + 4, AddButtonWidth, TrackRowHeight - 8);
        spriteBatch.Draw(pixel, addRect, new Color(70, 100, 130));
        InspectorDrawer.DrawLabel(spriteBatch, device, addRect.X + 10, addRect.Y + 2, "+", pixel);
        y = addRowY + TrackRowHeight;

        if (_addDropdownOpen && EventTrackRegistry.AllTypes.Count > 0)
        {
            int dropdownHeight = 4 + EventTrackRegistry.AllTypes.Count * TrackRowHeight;
            bool openUp = addRowY + TrackRowHeight + dropdownHeight > content.Bottom;
            int listY = openUp ? addRowY - dropdownHeight : addRowY + TrackRowHeight + 4;
            spriteBatch.Draw(pixel, new Rectangle(content.X + Padding, listY, content.Width - Padding * 2, dropdownHeight), new Color(42, 45, 50));
            listY += 4;
            foreach (var d in EventTrackRegistry.AllTypes)
            {
                var rowRect = new Rectangle(content.X + Padding, listY, content.Width - Padding * 2, TrackRowHeight);
                bool hover = rowRect.Contains(Input?.MousePosition ?? Point.Zero);
                spriteBatch.Draw(pixel, rowRect, hover ? new Color(55, 58, 65) : new Color(48, 51, 56));
                InspectorDrawer.DrawLabel(spriteBatch, device, rowRect.X + 6, rowRect.Y + 4, d.DisplayName, pixel);
                listY += TrackRowHeight;
            }
        }
    }
}
