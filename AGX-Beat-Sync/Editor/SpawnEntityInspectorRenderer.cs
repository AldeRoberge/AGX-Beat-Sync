using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Input;
using AGX_Beat_Sync.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.Editor;

public enum InspectorNumberField
{
    None,
    Speed,
    PositionX, PositionY, PositionZ,
    RotationX, RotationY, RotationZ,
}

public class SpawnEntityInspectorRenderer : IInspectorRenderer
{
    private bool _positionExpanded = true;
    private bool _rotationExpanded = true;
    private bool _positionDropdownOpen;
    private bool _rotationDropdownOpen;

    // Number field editing
    private InspectorNumberField _focusedField = InspectorNumberField.None;
    private string _editText = "";

    // Hit-test rects from last Draw (used in Update)
    private Rectangle _positionFoldoutRect;
    private Rectangle _positionModeRect;
    private Rectangle _rotationFoldoutRect;
    private Rectangle _rotationModeRect;
    private Rectangle _positionDropdownRect;
    private Rectangle[] _positionModeOptionRects = Array.Empty<Rectangle>();
    private Rectangle _rotationDropdownRect;
    private Rectangle[] _rotationModeOptionRects = Array.Empty<Rectangle>();
    private Rectangle _speedValueRect;
    private Rectangle[] _positionValueRects = Array.Empty<Rectangle>();
    private Rectangle[] _rotationValueRects = Array.Empty<Rectangle>();

    private static readonly string[] PositionModeOptions = Enum.GetNames<PositionMode>();
    private static readonly string[] RotationModeOptions = Enum.GetNames<RotationMode>();

    private static char? GetNumericKeyChar(Keys key)
    {
        return key switch
        {
            Keys.D0 or Keys.NumPad0 => '0',
            Keys.D1 or Keys.NumPad1 => '1',
            Keys.D2 or Keys.NumPad2 => '2',
            Keys.D3 or Keys.NumPad3 => '3',
            Keys.D4 or Keys.NumPad4 => '4',
            Keys.D5 or Keys.NumPad5 => '5',
            Keys.D6 or Keys.NumPad6 => '6',
            Keys.D7 or Keys.NumPad7 => '7',
            Keys.D8 or Keys.NumPad8 => '8',
            Keys.D9 or Keys.NumPad9 => '9',
            Keys.OemMinus => '-',
            Keys.OemPeriod or Keys.Decimal => '.',
            Keys.OemComma => '.',
            _ => null
        };
    }

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
        if (!_positionExpanded)
            _positionValueRects = Array.Empty<Rectangle>();

        if (_positionExpanded)
        {
            string posModeText = t.PositionMode.ToString();
            _positionModeRect = InspectorDrawer.DrawEnumRow(sb, pixel, sb.GraphicsDevice, x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, "Mode", posModeText, ref cursorY);
            y = cursorY;

            if (_positionDropdownOpen)
            {
                int selected = (int)t.PositionMode;
                (_positionDropdownRect, _positionModeOptionRects) = InspectorDrawer.DrawDropdownList(sb, pixel, sb.GraphicsDevice, x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, PositionModeOptions, selected, ref cursorY, input.MousePosition);
                y = cursorY;
                _positionValueRects = Array.Empty<Rectangle>();
            }
            else
                _positionModeOptionRects = Array.Empty<Rectangle>();

            if (!_positionDropdownOpen && t.PositionMode == PositionMode.Absolute)
            {
                var v = t.PositionAbsolute;
                GetVector3ValueRects(x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, out _positionValueRects);
                InspectorDrawer.DrawVector3Rows(sb, pixel, sb.GraphicsDevice, x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, v, ref cursorY,
                    _focusedField == InspectorNumberField.PositionX ? _editText : null,
                    _focusedField == InspectorNumberField.PositionY ? _editText : null,
                    _focusedField == InspectorNumberField.PositionZ ? _editText : null);
            }
            else if (!_positionDropdownOpen && t.PositionMode == PositionMode.Relative)
            {
                var v = t.PositionRelative;
                GetVector3ValueRects(x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, out _positionValueRects);
                InspectorDrawer.DrawVector3Rows(sb, pixel, sb.GraphicsDevice, x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, v, ref cursorY,
                    _focusedField == InspectorNumberField.PositionX ? _editText : null,
                    _focusedField == InspectorNumberField.PositionY ? _editText : null,
                    _focusedField == InspectorNumberField.PositionZ ? _editText : null);
            }
            else
                _positionValueRects = Array.Empty<Rectangle>();
        }

        y = cursorY;
        InspectorDrawer.DrawSeparator(sb, pixel, x, y, w, ref cursorY);
        y = cursorY;

        // Rotation section
        _rotationFoldoutRect = InspectorDrawer.DrawFoldout(sb, pixel, sb.GraphicsDevice, x, y, w, "Rotation", _rotationExpanded, ref cursorY);
        y = cursorY;
        if (!_rotationExpanded)
            _rotationValueRects = Array.Empty<Rectangle>();

        if (_rotationExpanded)
        {
            string rotModeText = t.RotationMode.ToString();
            _rotationModeRect = InspectorDrawer.DrawEnumRow(sb, pixel, sb.GraphicsDevice, x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, "Mode", rotModeText, ref cursorY);
            y = cursorY;

            if (_rotationDropdownOpen)
            {
                int selected = (int)t.RotationMode;
                (_rotationDropdownRect, _rotationModeOptionRects) = InspectorDrawer.DrawDropdownList(sb, pixel, sb.GraphicsDevice, x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, RotationModeOptions, selected, ref cursorY, input.MousePosition);
                y = cursorY;
                _rotationValueRects = Array.Empty<Rectangle>();
            }
            else
                _rotationModeOptionRects = Array.Empty<Rectangle>();

            if (!_rotationDropdownOpen && t.RotationMode == RotationMode.Absolute)
            {
                var v = t.RotationEuler;
                GetVector3ValueRects(x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, out _rotationValueRects);
                InspectorDrawer.DrawVector3Rows(sb, pixel, sb.GraphicsDevice, x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, v, ref cursorY,
                    _focusedField == InspectorNumberField.RotationX ? _editText : null,
                    _focusedField == InspectorNumberField.RotationY ? _editText : null,
                    _focusedField == InspectorNumberField.RotationZ ? _editText : null);
            }
            else
                _rotationValueRects = Array.Empty<Rectangle>();
        }

        y = cursorY;
        InspectorDrawer.DrawSeparator(sb, pixel, x, y, w, ref cursorY);
        y = cursorY;

        // Speed
        string speedText = _focusedField == InspectorNumberField.Speed ? _editText : t.Speed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        _speedValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Speed", speedText, ref cursorY);
    }

    private static void GetVector3ValueRects(int x, int y, int w, out Rectangle[] rects)
    {
        int valueW = Math.Max(60, w / 2);
        rects = new Rectangle[3];
        for (int i = 0; i < 3; i++)
            rects[i] = new Rectangle(x + w - valueW - InspectorDrawer.Padding, y + 2 + i * InspectorDrawer.RowHeight, valueW, InspectorDrawer.RowHeight - 4);
    }

    /// <summary>Compute the Mode value-button rect for hit-test without relying on Draw order. Matches InspectorDrawer layout.</summary>
    private Rectangle GetPositionModeValueRect(Rectangle contentArea)
    {
        int x = contentArea.X + InspectorDrawer.Padding + InspectorDrawer.Indent;
        int w = contentArea.Width - InspectorDrawer.Padding * 2 - InspectorDrawer.Indent;
        int y = contentArea.Y + InspectorDrawer.Padding + 22 + 1 + InspectorDrawer.RowHeight; // header + separator + foldout
        int valueW = Math.Max(80, w - InspectorDrawer.LabelWidth - InspectorDrawer.Padding * 2);
        return new Rectangle(x + w - valueW - InspectorDrawer.Padding, y + 2, valueW, InspectorDrawer.RowHeight - 4);
    }

    private Rectangle GetRotationModeValueRect(Rectangle contentArea, SpawnEntityTrack track)
    {
        int x = contentArea.X + InspectorDrawer.Padding + InspectorDrawer.Indent;
        int w = contentArea.Width - InspectorDrawer.Padding * 2 - InspectorDrawer.Indent;
        int y = contentArea.Y + InspectorDrawer.Padding + 22 + 1 + InspectorDrawer.RowHeight; // after position foldout
        y += _positionExpanded ? InspectorDrawer.RowHeight : 0; // position mode row
        if (_positionExpanded)
        {
            if (_positionDropdownOpen)
                y += 3 * InspectorDrawer.RowHeight; // dropdown list height
            else if (track.PositionMode is PositionMode.Absolute or PositionMode.Relative)
                y += 3 * InspectorDrawer.RowHeight; // vector3 rows
        }
        y += 1 + InspectorDrawer.RowHeight; // separator + rotation foldout
        int valueW = Math.Max(80, w - InspectorDrawer.LabelWidth - InspectorDrawer.Padding * 2);
        return new Rectangle(x + w - valueW - InspectorDrawer.Padding, y + 2, valueW, InspectorDrawer.RowHeight - 4);
    }

    public void Update(IEventTrack track, InputManager input, Rectangle contentArea)
    {
        if (track is not SpawnEntityTrack t)
            return;

        var pt = input.MousePosition;
        bool insideContent = contentArea.Contains(pt);

        // While a number field is focused, handle keyboard input
        if (_focusedField != InspectorNumberField.None)
        {
            if (input.IsKeyPressed(Keys.Back))
            {
                if (_editText.Length > 0)
                    _editText = _editText[..^1];
            }
            else if (input.IsKeyPressed(Keys.Enter) || input.IsKeyPressed(Keys.Escape))
            {
                if (input.IsKeyPressed(Keys.Enter))
                    TryCommitField(t);
                _focusedField = InspectorNumberField.None;
                return;
            }
            else
            {
                foreach (Keys key in Enum.GetValues<Keys>())
                {
                    if (input.IsKeyPressed(key) && GetNumericKeyChar(key) is char c)
                    {
                        if (c == '-' && _editText.Length > 0) continue; // only allow minus at start
                        if (c == '.' && _editText.Contains('.')) continue;
                        _editText += c;
                        break;
                    }
                }
            }

            // Click outside number fields: commit and clear focus (then fall through so foldout/dropdown can get the click)
            if (input.MouseLeftPressed)
            {
                if (!IsPointInAnyNumberField(pt))
                {
                    TryCommitField(t);
                    _focusedField = InspectorNumberField.None;
                    // Fall through to allow same click to hit foldout/dropdown
                }
                else
                {
                    // Click on a (possibly different) number field: commit current and focus the clicked one
                    var clicked = GetNumberFieldAt(pt);
                    if (clicked != _focusedField)
                    {
                        TryCommitField(t);
                        _focusedField = clicked;
                        _editText = GetValueString(t, clicked);
                    }
                    return;
                }
            }
            else
                return; // while focused and no click, don't process foldout/dropdown
        }

        if (!input.MouseLeftPressed)
            return;
        if (!insideContent)
            return;

        // Click on a number field to start editing
        var fieldAt = GetNumberFieldAt(pt);
        if (fieldAt != InspectorNumberField.None)
        {
            _focusedField = fieldAt;
            _editText = GetValueString(t, fieldAt);
            return;
        }

        if (_positionFoldoutRect.Contains(pt))
        {
            _positionExpanded = !_positionExpanded;
            _positionDropdownOpen = false;
            return;
        }

        if (_rotationFoldoutRect.Contains(pt))
        {
            _rotationExpanded = !_rotationExpanded;
            _rotationDropdownOpen = false;
            return;
        }

        // When dropdown is open, only allow selecting an option or clicking outside to close — never cycle.
        if (_positionDropdownOpen)
        {
            if (_positionDropdownRect.Contains(pt))
            {
                for (int i = 0; i < _positionModeOptionRects.Length; i++)
                {
                    if (_positionModeOptionRects[i].Contains(pt))
                    {
                        t.PositionMode = (PositionMode)i;
                        _positionDropdownOpen = false;
                        return;
                    }
                }
            }
            else
                _positionDropdownOpen = false;
            return;
        }

        if (_rotationDropdownOpen)
        {
            if (_rotationDropdownRect.Contains(pt))
            {
                for (int i = 0; i < _rotationModeOptionRects.Length; i++)
                {
                    if (_rotationModeOptionRects[i].Contains(pt))
                    {
                        t.RotationMode = (RotationMode)i;
                        _rotationDropdownOpen = false;
                        return;
                    }
                }
            }
            else
                _rotationDropdownOpen = false;
            return;
        }

        // Dropdown closed: click on value opens dropdown only (never change value).
        Rectangle positionModeRect = GetPositionModeValueRect(contentArea);
        if (_positionExpanded && positionModeRect.Contains(pt))
        {
            _positionDropdownOpen = true;
            _rotationDropdownOpen = false;
            return;
        }

        Rectangle rotationModeRect = GetRotationModeValueRect(contentArea, t);
        if (_rotationExpanded && rotationModeRect.Contains(pt))
        {
            _rotationDropdownOpen = true;
            _positionDropdownOpen = false;
        }
    }

    private bool IsPointInAnyNumberField(Point pt)
    {
        if (_speedValueRect.Contains(pt)) return true;
        foreach (var r in _positionValueRects) if (r.Contains(pt)) return true;
        foreach (var r in _rotationValueRects) if (r.Contains(pt)) return true;
        return false;
    }

    private InspectorNumberField GetNumberFieldAt(Point pt)
    {
        if (_speedValueRect.Contains(pt)) return InspectorNumberField.Speed;
        if (_positionValueRects.Length >= 3)
        {
            if (_positionValueRects[0].Contains(pt)) return InspectorNumberField.PositionX;
            if (_positionValueRects[1].Contains(pt)) return InspectorNumberField.PositionY;
            if (_positionValueRects[2].Contains(pt)) return InspectorNumberField.PositionZ;
        }
        if (_rotationValueRects.Length >= 3)
        {
            if (_rotationValueRects[0].Contains(pt)) return InspectorNumberField.RotationX;
            if (_rotationValueRects[1].Contains(pt)) return InspectorNumberField.RotationY;
            if (_rotationValueRects[2].Contains(pt)) return InspectorNumberField.RotationZ;
        }
        return InspectorNumberField.None;
    }

    private static string GetValueString(SpawnEntityTrack t, InspectorNumberField field)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return field switch
        {
            InspectorNumberField.Speed => t.Speed.ToString("0.##", inv),
            InspectorNumberField.PositionX => (t.PositionMode == PositionMode.Absolute ? t.PositionAbsolute.X : t.PositionRelative.X).ToString("0.##", inv),
            InspectorNumberField.PositionY => (t.PositionMode == PositionMode.Absolute ? t.PositionAbsolute.Y : t.PositionRelative.Y).ToString("0.##", inv),
            InspectorNumberField.PositionZ => (t.PositionMode == PositionMode.Absolute ? t.PositionAbsolute.Z : t.PositionRelative.Z).ToString("0.##", inv),
            InspectorNumberField.RotationX => t.RotationEuler.X.ToString("0.##", inv),
            InspectorNumberField.RotationY => t.RotationEuler.Y.ToString("0.##", inv),
            InspectorNumberField.RotationZ => t.RotationEuler.Z.ToString("0.##", inv),
            _ => "0"
        };
    }

    private void TryCommitField(SpawnEntityTrack t)
    {
        if (_focusedField == InspectorNumberField.None) return;
        if (!float.TryParse(_editText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float value))
            return;
        switch (_focusedField)
        {
            case InspectorNumberField.Speed:
                t.Speed = Math.Max(0.001f, value);
                break;
            case InspectorNumberField.PositionX:
                if (t.PositionMode == PositionMode.Absolute) t.PositionAbsolute = new Vector3(value, t.PositionAbsolute.Y, t.PositionAbsolute.Z);
                else t.PositionRelative = new Vector3(value, t.PositionRelative.Y, t.PositionRelative.Z);
                break;
            case InspectorNumberField.PositionY:
                if (t.PositionMode == PositionMode.Absolute) t.PositionAbsolute = new Vector3(t.PositionAbsolute.X, value, t.PositionAbsolute.Z);
                else t.PositionRelative = new Vector3(t.PositionRelative.X, value, t.PositionRelative.Z);
                break;
            case InspectorNumberField.PositionZ:
                if (t.PositionMode == PositionMode.Absolute) t.PositionAbsolute = new Vector3(t.PositionAbsolute.X, t.PositionAbsolute.Y, value);
                else t.PositionRelative = new Vector3(t.PositionRelative.X, t.PositionRelative.Y, value);
                break;
            case InspectorNumberField.RotationX:
                t.RotationEuler = new Vector3(value, t.RotationEuler.Y, t.RotationEuler.Z);
                break;
            case InspectorNumberField.RotationY:
                t.RotationEuler = new Vector3(t.RotationEuler.X, value, t.RotationEuler.Z);
                break;
            case InspectorNumberField.RotationZ:
                t.RotationEuler = new Vector3(t.RotationEuler.X, t.RotationEuler.Y, value);
                break;
        }
    }
}
