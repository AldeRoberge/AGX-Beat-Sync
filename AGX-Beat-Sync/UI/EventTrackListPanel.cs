using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Editor;
using AGX_Beat_Sync.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.UI;

/// <summary>
/// Left panel: vertical list of event tracks (DAW/piano style). Dropdown to pick type + Add button; click track to select; remove via button or Delete key.
/// </summary>
public class EventTrackListPanel : PanelBase
{
    private const int HeaderRowHeight = 32;
    private const int TrackRowHeight = 26;
    private const int DropdownWidth = 130;
    private const int AddButtonWidth = 36;
    private const int RemoveButtonWidth = 22;
    private const int Padding = 8;

    private bool _dropdownOpen;
    private string _selectedTypeIdForAdd = "";

    public Project? Project { get; set; }
    public EditorSelection? Selection { get; set; }
    public InputManager? Input { get; set; }

    public EventTrackListPanel()
    {
        Title = "Tracks";
        BackgroundColor = new Color(32, 34, 38);
    }

    public override string? GetHoverText(Point mouse)
    {
        if (Project == null || !ContentBounds.Contains(mouse)) return null;
        var content = ContentBounds;
        int y = content.Y;
        var dropdownRect = new Rectangle(content.X + Padding, y + 4, DropdownWidth, HeaderRowHeight - 8);
        if (dropdownRect.Contains(mouse)) return "Track type dropdown";
        var addRect = new Rectangle(content.X + Padding + DropdownWidth + 4, y + 4, AddButtonWidth, HeaderRowHeight - 8);
        if (addRect.Contains(mouse)) return "Add track";
        y += HeaderRowHeight + (_dropdownOpen ? EventTrackRegistry.AllTypes.Count * TrackRowHeight + 8 : 0);
        foreach (var track in Project.EventTracks)
        {
            var rowRect = new Rectangle(content.X, y, content.Width, TrackRowHeight);
            if (rowRect.Contains(mouse))
            {
                var removeRect = new Rectangle(rowRect.Right - RemoveButtonWidth - 2, y + 2, RemoveButtonWidth, TrackRowHeight - 4);
                if (removeRect.Contains(mouse)) return $"Remove track: {track.DisplayName}";
                return $"Track: {track.DisplayName}";
            }
            y += TrackRowHeight;
        }
        return "Tracks";
    }

    public override void Update(GameTime gameTime)
    {
        if (Project == null || Selection == null || Input == null)
            return;

        var content = ContentBounds;

        // Ensure we have a default type for add
        if (string.IsNullOrEmpty(_selectedTypeIdForAdd) && EventTrackRegistry.AllTypes.Count > 0)
            _selectedTypeIdForAdd = EventTrackRegistry.AllTypes[0].TrackTypeId;

        if (!Input.MouseLeftPressed)
            return;

        if (!content.Contains(Input.MousePosition))
        {
            _dropdownOpen = false;
            return;
        }

        int y = content.Y;

        // Dropdown area
        var dropdownRect = new Rectangle(content.X + Padding, y + 4, DropdownWidth, HeaderRowHeight - 8);
        if (dropdownRect.Contains(Input.MousePosition))
        {
            _dropdownOpen = !_dropdownOpen;
            return;
        }

        // Add button
        var addRect = new Rectangle(content.X + Padding + DropdownWidth + 4, y + 4, AddButtonWidth, HeaderRowHeight - 8);
        if (addRect.Contains(Input.MousePosition))
        {
            _dropdownOpen = false;
            if (!string.IsNullOrEmpty(_selectedTypeIdForAdd))
            {
                var track = EventTrackRegistry.CreateTrack(_selectedTypeIdForAdd);
                track.Order = Project.EventTracks.Count;
                Project.EventTracks.Add(track);
                Selection.SelectedEventTrack = track;
            }
            return;
        }

        if (_dropdownOpen)
        {
            int listY = content.Y + HeaderRowHeight + 4;
            foreach (var desc in EventTrackRegistry.AllTypes)
            {
                var rowRect = new Rectangle(content.X + Padding, listY, content.Width - Padding * 2, TrackRowHeight);
                if (rowRect.Contains(Input.MousePosition))
                {
                    _selectedTypeIdForAdd = desc.TrackTypeId;
                    _dropdownOpen = false;
                    return;
                }
                listY += TrackRowHeight;
            }
        }

        // Track list rows
        int trackIndex = 0;
        int trackY = content.Y + HeaderRowHeight + (_dropdownOpen ? (EventTrackRegistry.AllTypes.Count * TrackRowHeight + 8) : 0);
        foreach (var track in Project.EventTracks)
        {
            var rowRect = new Rectangle(content.X, trackY, content.Width, TrackRowHeight);
            var removeRect = new Rectangle(rowRect.Right - RemoveButtonWidth - 2, trackY + 2, RemoveButtonWidth, TrackRowHeight - 4);

            if (removeRect.Contains(Input.MousePosition))
            {
                Project.EventTracks.Remove(track);
                if (Selection.SelectedEventTrack == track)
                    Selection.SelectedEventTrack = Project.EventTracks.FirstOrDefault();
                return;
            }

            if (rowRect.Contains(Input.MousePosition) && !removeRect.Contains(Input.MousePosition))
            {
                Selection.SelectedEventTrack = track;
                _dropdownOpen = false;
                return;
            }

            trackY += TrackRowHeight;
            trackIndex++;
        }

        if (_dropdownOpen && Input.MousePosition.Y > content.Y + HeaderRowHeight + EventTrackRegistry.AllTypes.Count * TrackRowHeight + 8)
            _dropdownOpen = false;
    }

    protected override void DrawContent(SpriteBatch spriteBatch)
    {
        if (Project == null)
            return;

        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);
        var content = ContentBounds;
        var device = spriteBatch.GraphicsDevice;

        int y = content.Y;

        // Top bar: dropdown + Add
        spriteBatch.Draw(pixel, new Rectangle(content.X, y, content.Width, HeaderRowHeight), new Color(45, 48, 54));
        var dropdownRect = new Rectangle(content.X + Padding, y + 4, DropdownWidth, HeaderRowHeight - 8);
        spriteBatch.Draw(pixel, dropdownRect, new Color(58, 62, 70));
        spriteBatch.Draw(pixel, new Rectangle(dropdownRect.X - 1, dropdownRect.Y - 1, dropdownRect.Width + 2, dropdownRect.Height + 2), new Color(70, 74, 82));

        string dropdownText = _selectedTypeIdForAdd;
        var desc = EventTrackRegistry.AllTypes.FirstOrDefault(d => d.TrackTypeId == _selectedTypeIdForAdd);
        if (desc != null)
            dropdownText = desc.DisplayName;
        InspectorDrawer.DrawLabel(spriteBatch, device, dropdownRect.X + 6, dropdownRect.Y + 4, dropdownText.Length > 14 ? dropdownText[..14] + "…" : dropdownText, pixel);

        var addRect = new Rectangle(content.X + Padding + DropdownWidth + 4, y + 4, AddButtonWidth, HeaderRowHeight - 8);
        spriteBatch.Draw(pixel, addRect, new Color(70, 100, 130));
        InspectorDrawer.DrawLabel(spriteBatch, device, addRect.X + 10, addRect.Y + 2, "+", pixel);

        y += HeaderRowHeight;

        // Dropdown list when open
        if (_dropdownOpen && EventTrackRegistry.AllTypes.Count > 0)
        {
            spriteBatch.Draw(pixel, new Rectangle(content.X + Padding, y, content.Width - Padding * 2, EventTrackRegistry.AllTypes.Count * TrackRowHeight + 4), new Color(42, 45, 50));
            y += 4;
            foreach (var d in EventTrackRegistry.AllTypes)
            {
                var rowRect = new Rectangle(content.X + Padding, y, content.Width - Padding * 2, TrackRowHeight);
                bool hover = rowRect.Contains(Input?.MousePosition ?? Point.Zero);
                spriteBatch.Draw(pixel, rowRect, hover ? new Color(55, 58, 65) : new Color(48, 51, 56));
                InspectorDrawer.DrawLabel(spriteBatch, device, rowRect.X + 6, rowRect.Y + 4, d.DisplayName, pixel);
                y += TrackRowHeight;
            }
            y += 4;
        }

        // Track list
        int index = 0;
        foreach (var track in Project.EventTracks)
        {
            bool selected = Selection?.SelectedEventTrack == track;
            var rowRect = new Rectangle(content.X, y, content.Width, TrackRowHeight);
            var rowBg = selected ? new Color(62, 68, 78) : (index % 2 == 0 ? new Color(38, 40, 44) : new Color(35, 37, 41));
            spriteBatch.Draw(pixel, rowRect, rowBg);

            InspectorDrawer.DrawLabel(spriteBatch, device, content.X + Padding + 4, y + 4, track.DisplayName.Length > 16 ? track.DisplayName[..16] + "…" : track.DisplayName, pixel);

            var removeRect = new Rectangle(rowRect.Right - RemoveButtonWidth - 2, y + 2, RemoveButtonWidth, TrackRowHeight - 4);
            spriteBatch.Draw(pixel, removeRect, new Color(80, 50, 50));
            InspectorDrawer.DrawLabel(spriteBatch, device, removeRect.X + 6, removeRect.Y + 2, "×", pixel);

            y += TrackRowHeight;
            index++;
        }
    }
}
