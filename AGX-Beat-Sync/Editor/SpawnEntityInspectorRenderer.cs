using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Input;
using AGX_Beat_Sync.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.Editor;

public class SpawnEntityInspectorRenderer : IInspectorRenderer
{
    private bool _positionExpanded = true;
    private bool _rotationExpanded = true;

    // Hit-test rects from last Draw (used in Update)
    private Rectangle _positionFoldoutRect;
    private Rectangle _positionModeRect;
    private Rectangle _rotationFoldoutRect;
    private Rectangle _rotationModeRect;

    public void Draw(SpriteBatch sb, Rectangle contentArea, IEventTrack track, InputManager input, ref int cursorY)
    {
        if (track is not SpawnEntityTrack t)
            return;

        var pixel = PanelBase.GetPixelTexture(sb.GraphicsDevice);
        int x = contentArea.X + InspectorDrawer.Padding;
        int y = contentArea.Y + InspectorDrawer.Padding;
        int w = contentArea.Width - InspectorDrawer.Padding * 2;

        InspectorDrawer.DrawHeader(sb, pixel, sb.GraphicsDevice, x, y, w, t.DisplayName, ref cursorY);
        y = cursorY;
        InspectorDrawer.DrawSeparator(sb, pixel, x, y, w, ref cursorY);
        y = cursorY;

        // Position section
        _positionFoldoutRect = InspectorDrawer.DrawFoldout(sb, pixel, sb.GraphicsDevice, x, y, w, "Position", _positionExpanded, ref cursorY);
        y = cursorY;

        if (_positionExpanded)
        {
            string posModeText = t.PositionMode.ToString();
            _positionModeRect = InspectorDrawer.DrawEnumRow(sb, pixel, sb.GraphicsDevice, x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, "Mode", posModeText, ref cursorY);
            y = cursorY;

            if (t.PositionMode == PositionMode.Absolute)
                InspectorDrawer.DrawVector3Rows(sb, pixel, sb.GraphicsDevice, x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, t.PositionAbsolute, ref cursorY);
            else if (t.PositionMode == PositionMode.Relative)
                InspectorDrawer.DrawVector3Rows(sb, pixel, sb.GraphicsDevice, x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, t.PositionRelative, ref cursorY);
        }

        y = cursorY;
        InspectorDrawer.DrawSeparator(sb, pixel, x, y, w, ref cursorY);
        y = cursorY;

        // Rotation section
        _rotationFoldoutRect = InspectorDrawer.DrawFoldout(sb, pixel, sb.GraphicsDevice, x, y, w, "Rotation", _rotationExpanded, ref cursorY);
        y = cursorY;

        if (_rotationExpanded)
        {
            string rotModeText = t.RotationMode.ToString();
            _rotationModeRect = InspectorDrawer.DrawEnumRow(sb, pixel, sb.GraphicsDevice, x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, "Mode", rotModeText, ref cursorY);
            y = cursorY;

            if (t.RotationMode == RotationMode.Absolute)
                InspectorDrawer.DrawVector3Rows(sb, pixel, sb.GraphicsDevice, x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, t.RotationEuler, ref cursorY);
        }

        y = cursorY;
        InspectorDrawer.DrawSeparator(sb, pixel, x, y, w, ref cursorY);
        y = cursorY;

        // Speed
        InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Speed", t.Speed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), ref cursorY);
    }

    public void Update(IEventTrack track, InputManager input, Rectangle contentArea)
    {
        if (track is not SpawnEntityTrack t)
            return;

        if (!input.MouseLeftPressed)
            return;

        var pt = input.MousePosition;
        if (!contentArea.Contains(pt))
            return;

        if (_positionFoldoutRect.Contains(pt))
        {
            _positionExpanded = !_positionExpanded;
            return;
        }

        if (_rotationFoldoutRect.Contains(pt))
        {
            _rotationExpanded = !_rotationExpanded;
            return;
        }

        if (_positionExpanded && _positionModeRect.Contains(pt))
        {
            t.PositionMode = t.PositionMode switch
            {
                PositionMode.Origin => PositionMode.Absolute,
                PositionMode.Absolute => PositionMode.Relative,
                _ => PositionMode.Origin
            };
            return;
        }

        if (_rotationExpanded && _rotationModeRect.Contains(pt))
        {
            t.RotationMode = t.RotationMode == RotationMode.Absolute ? RotationMode.Towards : RotationMode.Absolute;
        }
    }
}
