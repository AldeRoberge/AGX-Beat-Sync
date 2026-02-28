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
    Lifetime,
    Count,
    CircleRadius, CircleSpread,
    ConeSpreadAngle,
    LineLength,
    OscillationAmplitude,
    OrbitingDistance,
    PositionX, PositionY, PositionZ,
    RotationX, RotationY, RotationZ,
}

public class SpawnEntityInspectorRenderer : IInspectorRenderer
{
    private bool _positionExpanded = true;
    private bool _rotationExpanded = true;
    private bool _advancedExpanded;
    private bool _positionDropdownOpen;
    private bool _rotationDropdownOpen;
    private bool _modeDropdownOpen;
    private bool _patternDropdownOpen;
    private bool _directionPatternDropdownOpen;

    // Number field editing
    private InspectorNumberField _focusedField = InspectorNumberField.None;
    private string _editText = "";

    // Hit-test rects from last Draw (used in Update)
    private Rectangle _positionFoldoutRect;
    private Rectangle _positionModeRect;
    private Rectangle _rotationFoldoutRect;
    private Rectangle _rotationModeRect;
    private Rectangle _advancedFoldoutRect;
    private Rectangle _positionDropdownRect;
    private Rectangle[] _positionModeOptionRects = Array.Empty<Rectangle>();
    private Rectangle _rotationDropdownRect;
    private Rectangle[] _rotationModeOptionRects = Array.Empty<Rectangle>();
    private Rectangle _modeValueRect;
    private Rectangle _modeDropdownRect;
    private Rectangle[] _modeOptionRects = Array.Empty<Rectangle>();
    private Rectangle _patternValueRect;
    private Rectangle _patternDropdownRect;
    private Rectangle[] _patternOptionRects = Array.Empty<Rectangle>();
    private Rectangle _speedValueRect;
    private Rectangle _lifetimeValueRect;
    private Rectangle _countValueRect;
    private Rectangle _circleRadiusValueRect;
    private Rectangle _circleSpreadValueRect;
    private Rectangle _coneSpreadAngleValueRect;
    private Rectangle _lineLengthValueRect;
    private Rectangle _directionPatternValueRect;
    private Rectangle _directionPatternDropdownRect;
    private Rectangle[] _directionPatternOptionRects = Array.Empty<Rectangle>();
    private Rectangle _oscillationAmplitudeValueRect;
    private Rectangle _orbitingDistanceValueRect;
    private Rectangle _fullCircleRect;
    private Rectangle[] _positionValueRects = Array.Empty<Rectangle>();
    private Rectangle[] _rotationValueRects = Array.Empty<Rectangle>();

    private static readonly string[] PositionModeOptions = Enum.GetNames<PositionMode>();
    private static readonly string[] RotationModeOptions = Enum.GetNames<RotationMode>();
    private static readonly string[] SpawnModeOptions = Enum.GetNames<SpawnMode>();
    private static readonly string[] SpawnPatternOptions = Enum.GetNames<SpawnPattern>();
    private static readonly string[] ProjectileDirectionPatternOptions = Enum.GetNames<ProjectileDirectionPattern>();

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

        // Mode: Single | Multiple
        string modeText = t.SpawnMode.ToString();
        _modeValueRect = InspectorDrawer.DrawEnumRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Mode", modeText, ref cursorY);
        y = cursorY;
        if (_modeDropdownOpen)
        {
            int selected = (int)t.SpawnMode;
            (_modeDropdownRect, _modeOptionRects) = InspectorDrawer.DrawDropdownList(sb, pixel, sb.GraphicsDevice, x, y, w, SpawnModeOptions, selected, ref cursorY, input.MousePosition);
            y = cursorY;
        }
        else
            _modeOptionRects = Array.Empty<Rectangle>();

        if (t.SpawnMode != SpawnMode.Multiple)
        {
            _countValueRect = default;
            _circleRadiusValueRect = default;
            _circleSpreadValueRect = default;
            _coneSpreadAngleValueRect = default;
            _lineLengthValueRect = default;
            _fullCircleRect = default;
            _patternValueRect = default;
        }

        // Pattern (only when Multiple)
        if (t.SpawnMode == SpawnMode.Multiple)
        {
            string patternText = t.Pattern.ToString();
            _patternValueRect = InspectorDrawer.DrawEnumRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Pattern", patternText, ref cursorY);
            y = cursorY;
            if (_patternDropdownOpen)
            {
                int selected = (int)t.Pattern;
                (_patternDropdownRect, _patternOptionRects) = InspectorDrawer.DrawDropdownList(sb, pixel, sb.GraphicsDevice, x, y, w, SpawnPatternOptions, selected, ref cursorY, input.MousePosition);
                y = cursorY;
            }
            else
                _patternOptionRects = Array.Empty<Rectangle>();

            // Count (1-10)
            int count = Math.Clamp(t.Count, 1, 10);
            string countText = _focusedField == InspectorNumberField.Count ? _editText : count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            bool countCursor = _focusedField == InspectorNumberField.Count && (Environment.TickCount64 / 500) % 2 == 0;
            _countValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Count", countText, ref cursorY, showCaret: countCursor);
            y = cursorY;

            // Pattern-specific params
            if (t.Pattern == SpawnPattern.Circle)
            {
                _coneSpreadAngleValueRect = default;
                _lineLengthValueRect = default;
                string radiusText = _focusedField == InspectorNumberField.CircleRadius ? _editText : t.CircleRadius.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                _circleRadiusValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Radius", radiusText, ref cursorY, showCaret: _focusedField == InspectorNumberField.CircleRadius && (Environment.TickCount64 / 500) % 2 == 0);
                y = cursorY;
                string fullCircleText = t.CircleFullCircle ? "On" : "Off";
                _fullCircleRect = InspectorDrawer.DrawEnumRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Full Circle", fullCircleText, ref cursorY);
                y = cursorY;
                if (!t.CircleFullCircle)
                {
                    string spreadText = _focusedField == InspectorNumberField.CircleSpread ? _editText : t.CircleSpread.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    _circleSpreadValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Spread", spreadText, ref cursorY, showCaret: _focusedField == InspectorNumberField.CircleSpread && (Environment.TickCount64 / 500) % 2 == 0);
                    y = cursorY;
                }
                else
                    _circleSpreadValueRect = default;
            }
            else if (t.Pattern == SpawnPattern.Cone)
            {
                _circleRadiusValueRect = default;
                _circleSpreadValueRect = default;
                _fullCircleRect = default;
                _lineLengthValueRect = default;
                string coneText = _focusedField == InspectorNumberField.ConeSpreadAngle ? _editText : t.ConeSpreadAngle.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                _coneSpreadAngleValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Spread Angle", coneText, ref cursorY, showCaret: _focusedField == InspectorNumberField.ConeSpreadAngle && (Environment.TickCount64 / 500) % 2 == 0);
                y = cursorY;
            }
            else if (t.Pattern == SpawnPattern.Line)
            {
                _circleRadiusValueRect = default;
                _circleSpreadValueRect = default;
                _fullCircleRect = default;
                _coneSpreadAngleValueRect = default;
                string lineText = _focusedField == InspectorNumberField.LineLength ? _editText : t.LineLength.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                _lineLengthValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Length", lineText, ref cursorY, showCaret: _focusedField == InspectorNumberField.LineLength && (Environment.TickCount64 / 500) % 2 == 0);
                y = cursorY;
            }
        }

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

            bool cursorVisible = (Environment.TickCount64 / 500) % 2 == 0;
            if (!_positionDropdownOpen && t.PositionMode == PositionMode.Absolute)
            {
                var v = t.PositionAbsolute;
                GetVector3ValueRects(x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, out _positionValueRects);
                InspectorDrawer.DrawVector3Rows(sb, pixel, sb.GraphicsDevice, x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, v, ref cursorY,
                    _focusedField == InspectorNumberField.PositionX ? _editText : null,
                    _focusedField == InspectorNumberField.PositionY ? _editText : null,
                    _focusedField == InspectorNumberField.PositionZ ? _editText : null,
                    cursorVisible && _focusedField == InspectorNumberField.PositionX,
                    cursorVisible && _focusedField == InspectorNumberField.PositionY,
                    cursorVisible && _focusedField == InspectorNumberField.PositionZ);
            }
            else if (!_positionDropdownOpen && t.PositionMode == PositionMode.Relative)
            {
                var v = t.PositionRelative;
                GetVector3ValueRects(x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, out _positionValueRects);
                InspectorDrawer.DrawVector3Rows(sb, pixel, sb.GraphicsDevice, x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, v, ref cursorY,
                    _focusedField == InspectorNumberField.PositionX ? _editText : null,
                    _focusedField == InspectorNumberField.PositionY ? _editText : null,
                    _focusedField == InspectorNumberField.PositionZ ? _editText : null,
                    cursorVisible && _focusedField == InspectorNumberField.PositionX,
                    cursorVisible && _focusedField == InspectorNumberField.PositionY,
                    cursorVisible && _focusedField == InspectorNumberField.PositionZ);
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

            bool rotationCursorVisible = (Environment.TickCount64 / 500) % 2 == 0;
            if (!_rotationDropdownOpen && t.RotationMode == RotationMode.Absolute)
            {
                var v = t.RotationEuler;
                GetVector3ValueRects(x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, out _rotationValueRects);
                InspectorDrawer.DrawVector3Rows(sb, pixel, sb.GraphicsDevice, x + InspectorDrawer.Indent, y, w - InspectorDrawer.Indent, v, ref cursorY,
                    _focusedField == InspectorNumberField.RotationX ? _editText : null,
                    _focusedField == InspectorNumberField.RotationY ? _editText : null,
                    _focusedField == InspectorNumberField.RotationZ ? _editText : null,
                    rotationCursorVisible && _focusedField == InspectorNumberField.RotationX,
                    rotationCursorVisible && _focusedField == InspectorNumberField.RotationY,
                    rotationCursorVisible && _focusedField == InspectorNumberField.RotationZ);
            }
            else
                _rotationValueRects = Array.Empty<Rectangle>();
        }

        y = cursorY;
        InspectorDrawer.DrawSeparator(sb, pixel, x, y, w, ref cursorY);
        y = cursorY;

        // Speed, Lifetime (universal)
        string speedText = _focusedField == InspectorNumberField.Speed ? _editText : t.Speed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        bool speedCursorVisible = _focusedField == InspectorNumberField.Speed && (Environment.TickCount64 / 500) % 2 == 0;
        _speedValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Speed", speedText, ref cursorY, showCaret: speedCursorVisible);
        y = cursorY;
        string lifetimeText = _focusedField == InspectorNumberField.Lifetime ? _editText : t.Lifetime.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        bool lifetimeCursorVisible = _focusedField == InspectorNumberField.Lifetime && (Environment.TickCount64 / 500) % 2 == 0;
        _lifetimeValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Lifetime", lifetimeText, ref cursorY, showCaret: lifetimeCursorVisible);
        y = cursorY;

        // Direction pattern (projectile movement)
        string directionPatternText = t.DirectionPattern.ToString();
        _directionPatternValueRect = InspectorDrawer.DrawEnumRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Direction", directionPatternText, ref cursorY);
        y = cursorY;
        if (_directionPatternDropdownOpen)
        {
            int selected = (int)t.DirectionPattern;
            (_directionPatternDropdownRect, _directionPatternOptionRects) = InspectorDrawer.DrawDropdownList(sb, pixel, sb.GraphicsDevice, x, y, w, ProjectileDirectionPatternOptions, selected, ref cursorY, input.MousePosition);
            y = cursorY;
        }
        else
            _directionPatternOptionRects = Array.Empty<Rectangle>();

        if (t.DirectionPattern == ProjectileDirectionPattern.Oscillation)
        {
            string ampText = _focusedField == InspectorNumberField.OscillationAmplitude ? _editText : t.OscillationAmplitude.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            _oscillationAmplitudeValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Amplitude (°)", ampText, ref cursorY, showCaret: _focusedField == InspectorNumberField.OscillationAmplitude && (Environment.TickCount64 / 500) % 2 == 0);
            y = cursorY;
            _orbitingDistanceValueRect = default;
        }
        else if (t.DirectionPattern == ProjectileDirectionPattern.Orbiting)
        {
            _oscillationAmplitudeValueRect = default;
            string distText = _focusedField == InspectorNumberField.OrbitingDistance ? _editText : t.OrbitingDistance.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            _orbitingDistanceValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Distance", distText, ref cursorY, showCaret: _focusedField == InspectorNumberField.OrbitingDistance && (Environment.TickCount64 / 500) % 2 == 0);
            y = cursorY;
        }
        else
        {
            _oscillationAmplitudeValueRect = default;
            _orbitingDistanceValueRect = default;
        }

        y = cursorY;
        InspectorDrawer.DrawSeparator(sb, pixel, x, y, w, ref cursorY);
        y = cursorY;
        _advancedFoldoutRect = InspectorDrawer.DrawFoldout(sb, pixel, sb.GraphicsDevice, x, y, w, "Advanced", _advancedExpanded, ref cursorY);
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

        if (_advancedFoldoutRect.Contains(pt))
        {
            _advancedExpanded = !_advancedExpanded;
            return;
        }

        // Mode dropdown
        if (_modeDropdownOpen)
        {
            if (_modeDropdownRect.Contains(pt))
            {
                for (int i = 0; i < _modeOptionRects.Length; i++)
                {
                    if (_modeOptionRects[i].Contains(pt))
                    {
                        t.SpawnMode = (SpawnMode)i;
                        _modeDropdownOpen = false;
                        return;
                    }
                }
            }
            else
                _modeDropdownOpen = false;
            return;
        }

        if (_patternDropdownOpen && t.SpawnMode == SpawnMode.Multiple)
        {
            if (_patternDropdownRect.Contains(pt))
            {
                for (int i = 0; i < _patternOptionRects.Length; i++)
                {
                    if (_patternOptionRects[i].Contains(pt))
                    {
                        t.Pattern = (SpawnPattern)i;
                        _patternDropdownOpen = false;
                        return;
                    }
                }
            }
            else
                _patternDropdownOpen = false;
            return;
        }

        if (_directionPatternDropdownOpen)
        {
            if (_directionPatternDropdownRect.Contains(pt))
            {
                for (int i = 0; i < _directionPatternOptionRects.Length; i++)
                {
                    if (_directionPatternOptionRects[i].Contains(pt))
                    {
                        t.DirectionPattern = (ProjectileDirectionPattern)i;
                        _directionPatternDropdownOpen = false;
                        return;
                    }
                }
            }
            else
                _directionPatternDropdownOpen = false;
            return;
        }

        if (t.SpawnMode == SpawnMode.Multiple && t.Pattern == SpawnPattern.Circle && _fullCircleRect != default && _fullCircleRect.Contains(pt))
        {
            t.CircleFullCircle = !t.CircleFullCircle;
            return;
        }

        if (!_modeDropdownOpen && _modeValueRect.Contains(pt))
        {
            _modeDropdownOpen = true;
            _positionDropdownOpen = false;
            _rotationDropdownOpen = false;
            _patternDropdownOpen = false;
            _directionPatternDropdownOpen = false;
            return;
        }

        if (t.SpawnMode == SpawnMode.Multiple && !_patternDropdownOpen && _patternValueRect != default && _patternValueRect.Contains(pt))
        {
            _patternDropdownOpen = true;
            _positionDropdownOpen = false;
            _rotationDropdownOpen = false;
            _modeDropdownOpen = false;
            _directionPatternDropdownOpen = false;
            return;
        }

        if (!_directionPatternDropdownOpen && _directionPatternValueRect != default && _directionPatternValueRect.Contains(pt))
        {
            _directionPatternDropdownOpen = true;
            _positionDropdownOpen = false;
            _rotationDropdownOpen = false;
            _modeDropdownOpen = false;
            _patternDropdownOpen = false;
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
            _directionPatternDropdownOpen = false;
            return;
        }

        Rectangle rotationModeRect = GetRotationModeValueRect(contentArea, t);
        if (_rotationExpanded && rotationModeRect.Contains(pt))
        {
            _rotationDropdownOpen = true;
            _positionDropdownOpen = false;
            _directionPatternDropdownOpen = false;
        }
    }

    private bool IsPointInAnyNumberField(Point pt)
    {
        if (_speedValueRect.Contains(pt)) return true;
        if (_lifetimeValueRect.Contains(pt)) return true;
        if (_oscillationAmplitudeValueRect != default && _oscillationAmplitudeValueRect.Contains(pt)) return true;
        if (_orbitingDistanceValueRect != default && _orbitingDistanceValueRect.Contains(pt)) return true;
        if (_countValueRect.Contains(pt) && _countValueRect != default) return true;
        if (_circleRadiusValueRect.Contains(pt) && _circleRadiusValueRect != default) return true;
        if (_circleSpreadValueRect.Contains(pt) && _circleSpreadValueRect != default) return true;
        if (_coneSpreadAngleValueRect.Contains(pt) && _coneSpreadAngleValueRect != default) return true;
        if (_lineLengthValueRect.Contains(pt) && _lineLengthValueRect != default) return true;
        foreach (var r in _positionValueRects) if (r.Contains(pt)) return true;
        foreach (var r in _rotationValueRects) if (r.Contains(pt)) return true;
        return false;
    }

    private InspectorNumberField GetNumberFieldAt(Point pt)
    {
        if (_speedValueRect.Contains(pt)) return InspectorNumberField.Speed;
        if (_lifetimeValueRect.Contains(pt)) return InspectorNumberField.Lifetime;
        if (_oscillationAmplitudeValueRect != default && _oscillationAmplitudeValueRect.Contains(pt)) return InspectorNumberField.OscillationAmplitude;
        if (_orbitingDistanceValueRect != default && _orbitingDistanceValueRect.Contains(pt)) return InspectorNumberField.OrbitingDistance;
        if (_countValueRect != default && _countValueRect.Contains(pt)) return InspectorNumberField.Count;
        if (_circleRadiusValueRect != default && _circleRadiusValueRect.Contains(pt)) return InspectorNumberField.CircleRadius;
        if (_circleSpreadValueRect != default && _circleSpreadValueRect.Contains(pt)) return InspectorNumberField.CircleSpread;
        if (_coneSpreadAngleValueRect != default && _coneSpreadAngleValueRect.Contains(pt)) return InspectorNumberField.ConeSpreadAngle;
        if (_lineLengthValueRect != default && _lineLengthValueRect.Contains(pt)) return InspectorNumberField.LineLength;
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
            InspectorNumberField.Lifetime => t.Lifetime.ToString("0.##", inv),
            InspectorNumberField.Count => Math.Clamp(t.Count, 1, 10).ToString(inv),
            InspectorNumberField.CircleRadius => t.CircleRadius.ToString("0.##", inv),
            InspectorNumberField.CircleSpread => t.CircleSpread.ToString("0.##", inv),
            InspectorNumberField.ConeSpreadAngle => t.ConeSpreadAngle.ToString("0.##", inv),
            InspectorNumberField.LineLength => t.LineLength.ToString("0.##", inv),
            InspectorNumberField.OscillationAmplitude => t.OscillationAmplitude.ToString("0.##", inv),
            InspectorNumberField.OrbitingDistance => t.OrbitingDistance.ToString("0.##", inv),
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
            case InspectorNumberField.Lifetime:
                t.Lifetime = Math.Max(0.001f, value);
                break;
            case InspectorNumberField.Count:
                t.Count = (int)Math.Clamp(Math.Round(value), 1, 10);
                break;
            case InspectorNumberField.CircleRadius:
                t.CircleRadius = Math.Max(0f, value);
                break;
            case InspectorNumberField.CircleSpread:
                t.CircleSpread = Math.Clamp(value, 1f, 360f);
                break;
            case InspectorNumberField.ConeSpreadAngle:
                t.ConeSpreadAngle = Math.Clamp(value, 0.1f, 360f);
                break;
            case InspectorNumberField.LineLength:
                t.LineLength = Math.Max(0.001f, value);
                break;
            case InspectorNumberField.OscillationAmplitude:
                t.OscillationAmplitude = Math.Clamp(value, 0.1f, 180f);
                break;
            case InspectorNumberField.OrbitingDistance:
                t.OrbitingDistance = Math.Max(0.001f, value);
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
