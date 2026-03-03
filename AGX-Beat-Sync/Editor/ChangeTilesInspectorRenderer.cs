using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Input;
using AGX_Beat_Sync.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.Editor;

public class ChangeTilesInspectorRenderer : IInspectorRenderer
{
    private bool _shapeDropdownOpen;
    private Rectangle _shapeValueRect;
    private Rectangle _shapeDropdownRect;
    private Rectangle[] _shapeOptionRects = Array.Empty<Rectangle>();

    private static readonly string[] ShapeOptions = Enum.GetNames<ChangeTilesShape>();

    public void Draw(SpriteBatch sb, Rectangle contentArea, IEventTrack track, InputManager input, ref int cursorY, EditorSelection? selection)
    {
        if (track is not ChangeTilesTrack t)
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
        ChangeTilesShape displayShape = ChangeTilesShape.Circle;
        if (hasNoteSelection && selection!.SelectedNotes.Count > 0)
        {
            var first = selection.SelectedNotes.First(n => n.Track == t);
            displayShape = t.GetShape(first.EventTime);
        }

        string shapeText = displayShape.ToString();
        _shapeValueRect = InspectorDrawer.DrawEnumRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Shape", shapeText, ref cursorY);
        y = cursorY;
        if (_shapeDropdownOpen)
        {
            int selected = (int)displayShape;
            (_shapeDropdownRect, _shapeOptionRects) = InspectorDrawer.DrawDropdownList(sb, pixel, sb.GraphicsDevice, x, y, w, ShapeOptions, selected, ref cursorY, input.MousePosition);
        }
        else
            _shapeOptionRects = Array.Empty<Rectangle>();

        if (!hasNoteSelection)
        {
            InspectorDrawer.DrawRowLabel(sb, pixel, sb.GraphicsDevice, x, y, w, "Select a note to set shape", ref cursorY);
        }
    }

    public void Update(IEventTrack track, InputManager input, Rectangle contentArea, EditorSelection? selection)
    {
        if (track is not ChangeTilesTrack t)
            return;

        var pt = input.MousePosition;
        if (!contentArea.Contains(pt) || selection?.SelectedNotes == null)
        {
            if (!input.MouseLeftPressed)
                return;
            _shapeDropdownOpen = false;
            return;
        }

        bool hasNoteSelection = selection.SelectedNotes.Count > 0 && selection.SelectedNotes.Any(n => n.Track == t);
        if (!hasNoteSelection)
        {
            if (input.MouseLeftPressed)
                _shapeDropdownOpen = false;
            return;
        }

        if (input.MouseLeftPressed)
        {
            if (_shapeDropdownOpen && _shapeOptionRects.Length > 0)
            {
                for (int i = 0; i < _shapeOptionRects.Length; i++)
                {
                    if (_shapeOptionRects[i].Contains(pt))
                    {
                        var newShape = (ChangeTilesShape)i;
                        foreach (var (noteTrack, eventTime) in selection.SelectedNotes)
                        {
                            if (noteTrack == t)
                                t.SetShape(eventTime, newShape);
                        }
                        _shapeDropdownOpen = false;
                        return;
                    }
                }
                _shapeDropdownOpen = false;
                return;
            }

            if (_shapeValueRect.Contains(pt))
            {
                _shapeDropdownOpen = true;
                return;
            }
            _shapeDropdownOpen = false;
        }
    }
}
