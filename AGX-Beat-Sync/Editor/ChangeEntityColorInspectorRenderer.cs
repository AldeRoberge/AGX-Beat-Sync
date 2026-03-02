using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Input;
using AGX_Beat_Sync.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.Editor;

public class ChangeEntityColorInspectorRenderer : IInspectorRenderer
{
    private bool _colorDropdownOpen;
    private Rectangle _colorValueRect;
    private Rectangle _colorDropdownRect;
    private Rectangle[] _colorOptionRects = Array.Empty<Rectangle>();

    private static readonly string[] ColorOptions = Enum.GetNames<EntityColor>();

    public void Draw(SpriteBatch sb, Rectangle contentArea, IEventTrack track, InputManager input, ref int cursorY, EditorSelection? selection)
    {
        if (track is not ChangeEntityColorTrack t)
            return;

        var pixel = PanelBase.GetPixelTexture(sb.GraphicsDevice);
        int x = contentArea.X + InspectorDrawer.Padding;
        int y = contentArea.Y + InspectorDrawer.Padding;
        int w = contentArea.Width - InspectorDrawer.Padding * 2;

        InspectorDrawer.DrawHeader(sb, pixel, sb.GraphicsDevice, x, y, w, t.DisplayName, ref cursorY);
        y = cursorY;
        InspectorDrawer.DrawSeparator(sb, pixel, x, y, w, ref cursorY);
        y = cursorY;

        bool hasNoteSelection = selection?.SelectedNotes.Count > 0 && selection.SelectedNotes.Any(n => n.Track == t);
        EntityColor displayColor = EntityColor.Red;
        if (hasNoteSelection && selection!.SelectedNotes.Count > 0)
        {
            var first = selection.SelectedNotes.First(n => n.Track == t);
            displayColor = t.GetColor(first.EventTime);
        }

        // Color preview swatch so the selected color is visible (Red, Green, Blue, etc.)
        var xnaColor = ChangeEntityColorTrack.ToXnaColor(displayColor);
        sb.Draw(pixel, new Rectangle(x, y, w, InspectorDrawer.RowHeight), InspectorDrawer.RowBg);
        InspectorDrawer.DrawLabel(sb, sb.GraphicsDevice, x + InspectorDrawer.Padding, y + 2, "Color", pixel);
        const int swatchSize = 20;
        var swatchRect = new Rectangle(x + w - InspectorDrawer.Padding - swatchSize - 80, y + (InspectorDrawer.RowHeight - swatchSize) / 2, swatchSize, swatchSize);
        sb.Draw(pixel, new Rectangle(swatchRect.X - 1, swatchRect.Y - 1, swatchRect.Width + 2, swatchRect.Height + 2), InspectorDrawer.ControlBorder);
        sb.Draw(pixel, swatchRect, xnaColor);
        cursorY = y + InspectorDrawer.RowHeight;
        y = cursorY;

        string colorText = displayColor.ToString();
        _colorValueRect = InspectorDrawer.DrawEnumRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Set color", colorText, ref cursorY);
        y = cursorY;
        if (_colorDropdownOpen)
        {
            int selected = (int)displayColor;
            (_colorDropdownRect, _colorOptionRects) = InspectorDrawer.DrawDropdownList(sb, pixel, sb.GraphicsDevice, x, y, w, ColorOptions, selected, ref cursorY, input.MousePosition);
        }
        else
            _colorOptionRects = Array.Empty<Rectangle>();

        if (!hasNoteSelection)
        {
            InspectorDrawer.DrawRowLabel(sb, pixel, sb.GraphicsDevice, x, y, w, "Select a note to set color", ref cursorY);
        }
    }

    public void Update(IEventTrack track, InputManager input, Rectangle contentArea, EditorSelection? selection)
    {
        if (track is not ChangeEntityColorTrack t)
            return;

        var pt = input.MousePosition;
        if (!contentArea.Contains(pt) || selection?.SelectedNotes == null)
        {
            if (!input.MouseLeftPressed)
                return;
            _colorDropdownOpen = false;
            return;
        }

        bool hasNoteSelection = selection.SelectedNotes.Count > 0 && selection.SelectedNotes.Any(n => n.Track == t);
        if (!hasNoteSelection)
        {
            if (input.MouseLeftPressed)
                _colorDropdownOpen = false;
            return;
        }

        if (input.MouseLeftPressed)
        {
            if (_colorDropdownOpen && _colorOptionRects.Length > 0)
            {
                for (int i = 0; i < _colorOptionRects.Length; i++)
                {
                    if (_colorOptionRects[i].Contains(pt))
                    {
                        var newColor = (EntityColor)i;
                        foreach (var (noteTrack, eventTime) in selection.SelectedNotes)
                        {
                            if (noteTrack == t)
                                t.SetColor(eventTime, newColor);
                        }
                        _colorDropdownOpen = false;
                        return;
                    }
                }
                _colorDropdownOpen = false;
                return;
            }

            if (_colorValueRect.Contains(pt))
            {
                _colorDropdownOpen = true;
                return;
            }
            _colorDropdownOpen = false;
        }
    }
}
