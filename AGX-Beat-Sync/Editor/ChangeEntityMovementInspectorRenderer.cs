using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Input;
using AGX_Beat_Sync.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.Editor;

public class ChangeEntityMovementInspectorRenderer : IInspectorRenderer
{
    private bool _movementDropdownOpen;
    private Rectangle _movementValueRect;
    private Rectangle _movementDropdownRect;
    private Rectangle[] _movementOptionRects = Array.Empty<Rectangle>();

    private static readonly string[] MovementOptions = Enum.GetNames<EntityMovementKind>();

    public void Draw(SpriteBatch sb, Rectangle contentArea, IEventTrack track, InputManager input, ref int cursorY, EditorSelection? selection)
    {
        if (track is not ChangeEntityMovementTrack t)
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
        EntityMovementKind displayMovement = EntityMovementKind.Stationary;
        if (hasNoteSelection && selection!.SelectedNotes.Count > 0)
        {
            var first = selection.SelectedNotes.First(n => n.Track == t);
            displayMovement = t.GetMovement(first.EventTime);
        }

        string movementText = displayMovement.ToString();
        _movementValueRect = InspectorDrawer.DrawEnumRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Movement", movementText, ref cursorY);
        y = cursorY;
        if (_movementDropdownOpen)
        {
            int selected = (int)displayMovement;
            (_movementDropdownRect, _movementOptionRects) = InspectorDrawer.DrawDropdownList(sb, pixel, sb.GraphicsDevice, x, y, w, MovementOptions, selected, ref cursorY, input.MousePosition);
        }
        else
            _movementOptionRects = Array.Empty<Rectangle>();

        if (!hasNoteSelection)
        {
            InspectorDrawer.DrawRowLabel(sb, pixel, sb.GraphicsDevice, x, y, w, "Select a note to set movement", ref cursorY);
        }
    }

    public void Update(IEventTrack track, InputManager input, Rectangle contentArea, EditorSelection? selection)
    {
        if (track is not ChangeEntityMovementTrack t)
            return;

        var pt = input.MousePosition;
        if (!contentArea.Contains(pt) || selection?.SelectedNotes == null)
        {
            if (!input.MouseLeftPressed)
                return;
            _movementDropdownOpen = false;
            return;
        }

        bool hasNoteSelection = selection.SelectedNotes.Count > 0 && selection.SelectedNotes.Any(n => n.Track == t);
        if (!hasNoteSelection)
        {
            if (input.MouseLeftPressed)
                _movementDropdownOpen = false;
            return;
        }

        if (input.MouseLeftPressed)
        {
            if (_movementDropdownOpen && _movementOptionRects.Length > 0)
            {
                for (int i = 0; i < _movementOptionRects.Length; i++)
                {
                    if (_movementOptionRects[i].Contains(pt))
                    {
                        var newMovement = (EntityMovementKind)i;
                        foreach (var (noteTrack, eventTime) in selection.SelectedNotes)
                        {
                            if (noteTrack == t)
                                t.SetMovement(eventTime, newMovement);
                        }
                        _movementDropdownOpen = false;
                        return;
                    }
                }
                _movementDropdownOpen = false;
                return;
            }

            if (_movementValueRect.Contains(pt))
            {
                _movementDropdownOpen = true;
                return;
            }
            _movementDropdownOpen = false;
        }
    }
}
