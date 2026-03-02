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

public enum RandomSubField { None, Low, High }

public class SpawnEntityInspectorRenderer : IInspectorRenderer
{
    private bool _positionExpanded = true;
    private bool _rotationExpanded = true;
    private bool _advancedExpanded;
    private bool _entityKindDropdownOpen;
    private bool _positionDropdownOpen;
    private bool _rotationDropdownOpen;
    private bool _modeDropdownOpen;
    private bool _patternDropdownOpen;
    private bool _directionPatternDropdownOpen;

    // Number field editing
    private InspectorNumberField _focusedField = InspectorNumberField.None;
    private RandomSubField _randomSubField = RandomSubField.None;
    private string _editText = "";

    // Random toggle and Low/High rects (set during Draw when field is visible and random on)
    private Rectangle _speedRandomButtonRect;
    private Rectangle _lifetimeRandomButtonRect;
    private Rectangle _countRandomButtonRect;
    private Rectangle _circleRadiusRandomButtonRect;
    private Rectangle _circleSpreadRandomButtonRect;
    private Rectangle _coneSpreadAngleRandomButtonRect;
    private Rectangle _lineLengthRandomButtonRect;
    private Rectangle _oscillationAmplitudeRandomButtonRect;
    private Rectangle _orbitingDistanceRandomButtonRect;
    private Rectangle _speedLowValueRect, _speedHighValueRect;
    private Rectangle _lifetimeLowValueRect, _lifetimeHighValueRect;
    private Rectangle _countLowValueRect, _countHighValueRect;
    private Rectangle _circleRadiusLowValueRect, _circleRadiusHighValueRect;
    private Rectangle _circleSpreadLowValueRect, _circleSpreadHighValueRect;
    private Rectangle _coneSpreadAngleLowValueRect, _coneSpreadAngleHighValueRect;
    private Rectangle _lineLengthLowValueRect, _lineLengthHighValueRect;
    private Rectangle _oscillationAmplitudeLowValueRect, _oscillationAmplitudeHighValueRect;
    private Rectangle _orbitingDistanceLowValueRect, _orbitingDistanceHighValueRect;

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
    private Rectangle _entityKindValueRect;
    private Rectangle _entityKindDropdownRect;
    private Rectangle[] _entityKindOptionRects = Array.Empty<Rectangle>();
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

    private static readonly string[] SpawnEntityKindOptions = Enum.GetNames<SpawnEntityKind>();
    private static readonly string[] PositionModeOptions = Enum.GetNames<PositionMode>();
    private static readonly string[] RotationModeOptions = Enum.GetNames<RotationMode>();
    private static readonly string[] SpawnModeOptions = Enum.GetNames<SpawnMode>();
    private static readonly string[] SpawnPatternOptions = Enum.GetNames<SpawnPattern>();
    private static readonly string[] ProjectileDirectionPatternOptions = Enum.GetNames<ProjectileDirectionPattern>();

    private static string GetRandomRangeKey(InspectorNumberField field)
    {
        return field switch
        {
            InspectorNumberField.Speed => "Speed",
            InspectorNumberField.Lifetime => "Lifetime",
            InspectorNumberField.Count => "Count",
            InspectorNumberField.CircleRadius => "CircleRadius",
            InspectorNumberField.CircleSpread => "CircleSpread",
            InspectorNumberField.ConeSpreadAngle => "ConeSpreadAngle",
            InspectorNumberField.LineLength => "LineLength",
            InspectorNumberField.OscillationAmplitude => "OscillationAmplitude",
            InspectorNumberField.OrbitingDistance => "OrbitingDistance",
            InspectorNumberField.PositionX => "PositionX",
            InspectorNumberField.PositionY => "PositionY",
            InspectorNumberField.PositionZ => "PositionZ",
            InspectorNumberField.RotationX => "RotationX",
            InspectorNumberField.RotationY => "RotationY",
            InspectorNumberField.RotationZ => "RotationZ",
            _ => ""
        };
    }

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

    public void Draw(SpriteBatch sb, Rectangle contentArea, IEventTrack track, InputManager input, ref int cursorY, EditorSelection? selection)
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

        // Entity type: SmallCube | Projectile
        string entityKindText = t.EntityKind.ToString();
        _entityKindValueRect = InspectorDrawer.DrawEnumRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Entity", entityKindText, ref cursorY);
        y = cursorY;
        if (_entityKindDropdownOpen)
        {
            int selected = (int)t.EntityKind;
            (_entityKindDropdownRect, _entityKindOptionRects) = InspectorDrawer.DrawDropdownList(sb, pixel, sb.GraphicsDevice, x, y, w, SpawnEntityKindOptions, selected, ref cursorY, input.MousePosition);
            y = cursorY;
        }
        else
            _entityKindOptionRects = Array.Empty<Rectangle>();

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

            // Count (1-10) with random toggle
            int count = Math.Clamp(t.Count, 1, 10);
            var countRange = t.RandomRanges.TryGetValue("Count", out var cr) ? cr : null;
            bool countRandomOn = countRange?.UseRandom ?? false;
            string countText = countRandomOn ? $"{countRange!.Low:0} .. {countRange.High:0}" : (_focusedField == InspectorNumberField.Count ? _editText : count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            bool countCursor = _focusedField == InspectorNumberField.Count && _randomSubField == RandomSubField.None && (Environment.TickCount64 / 500) % 2 == 0;
            (_countValueRect, _countRandomButtonRect) = InspectorDrawer.DrawFloatRowWithRandomToggle(sb, pixel, sb.GraphicsDevice, x, y, w, "Count", countText, countRandomOn, ref cursorY, showCaret: countCursor);
            y = cursorY;
            if (countRandomOn)
            {
                string lowText = _focusedField == InspectorNumberField.Count && _randomSubField == RandomSubField.Low ? _editText : ((int)Math.Round(countRange!.Low)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                string highText = _focusedField == InspectorNumberField.Count && _randomSubField == RandomSubField.High ? _editText : ((int)Math.Round(countRange!.High)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                _countLowValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Low", lowText, ref cursorY, showCaret: _focusedField == InspectorNumberField.Count && _randomSubField == RandomSubField.Low && (Environment.TickCount64 / 500) % 2 == 0);
                y = cursorY;
                _countHighValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "High", highText, ref cursorY, showCaret: _focusedField == InspectorNumberField.Count && _randomSubField == RandomSubField.High && (Environment.TickCount64 / 500) % 2 == 0);
                y = cursorY;
            }
            else
                _countLowValueRect = _countHighValueRect = default;

            // Pattern-specific params
            if (t.Pattern == SpawnPattern.Circle)
            {
                _coneSpreadAngleValueRect = default;
                _lineLengthValueRect = default;
                var radiusRange = t.RandomRanges.TryGetValue("CircleRadius", out var rr) ? rr : null;
                bool radiusRandomOn = radiusRange?.UseRandom ?? false;
                string radiusText = radiusRandomOn ? $"{radiusRange!.Low:0.##} .. {radiusRange.High:0.##}" : (_focusedField == InspectorNumberField.CircleRadius ? _editText : t.CircleRadius.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                (_circleRadiusValueRect, _circleRadiusRandomButtonRect) = InspectorDrawer.DrawFloatRowWithRandomToggle(sb, pixel, sb.GraphicsDevice, x, y, w, "Radius", radiusText, radiusRandomOn, ref cursorY, showCaret: _focusedField == InspectorNumberField.CircleRadius && _randomSubField == RandomSubField.None && (Environment.TickCount64 / 500) % 2 == 0);
                y = cursorY;
                if (radiusRandomOn)
                {
                    string lowText = _focusedField == InspectorNumberField.CircleRadius && _randomSubField == RandomSubField.Low ? _editText : radiusRange!.Low.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    string highText = _focusedField == InspectorNumberField.CircleRadius && _randomSubField == RandomSubField.High ? _editText : radiusRange!.High.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    _circleRadiusLowValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Low", lowText, ref cursorY, showCaret: _focusedField == InspectorNumberField.CircleRadius && _randomSubField == RandomSubField.Low && (Environment.TickCount64 / 500) % 2 == 0);
                    y = cursorY;
                    _circleRadiusHighValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "High", highText, ref cursorY, showCaret: _focusedField == InspectorNumberField.CircleRadius && _randomSubField == RandomSubField.High && (Environment.TickCount64 / 500) % 2 == 0);
                    y = cursorY;
                }
                else
                    _circleRadiusLowValueRect = _circleRadiusHighValueRect = default;
                string fullCircleText = t.CircleFullCircle ? "On" : "Off";
                _fullCircleRect = InspectorDrawer.DrawEnumRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Full Circle", fullCircleText, ref cursorY);
                y = cursorY;
                if (!t.CircleFullCircle)
                {
                    var spreadRange = t.RandomRanges.TryGetValue("CircleSpread", out var spr) ? spr : null;
                    bool spreadRandomOn = spreadRange?.UseRandom ?? false;
                    string spreadText = spreadRandomOn ? $"{spreadRange!.Low:0.##} .. {spreadRange.High:0.##}" : (_focusedField == InspectorNumberField.CircleSpread ? _editText : t.CircleSpread.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                    (_circleSpreadValueRect, _circleSpreadRandomButtonRect) = InspectorDrawer.DrawFloatRowWithRandomToggle(sb, pixel, sb.GraphicsDevice, x, y, w, "Spread", spreadText, spreadRandomOn, ref cursorY, showCaret: _focusedField == InspectorNumberField.CircleSpread && _randomSubField == RandomSubField.None && (Environment.TickCount64 / 500) % 2 == 0);
                    y = cursorY;
                    if (spreadRandomOn)
                    {
                        string lowText = _focusedField == InspectorNumberField.CircleSpread && _randomSubField == RandomSubField.Low ? _editText : spreadRange!.Low.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                        string highText = _focusedField == InspectorNumberField.CircleSpread && _randomSubField == RandomSubField.High ? _editText : spreadRange!.High.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                        _circleSpreadLowValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Low", lowText, ref cursorY, showCaret: _focusedField == InspectorNumberField.CircleSpread && _randomSubField == RandomSubField.Low && (Environment.TickCount64 / 500) % 2 == 0);
                        y = cursorY;
                        _circleSpreadHighValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "High", highText, ref cursorY, showCaret: _focusedField == InspectorNumberField.CircleSpread && _randomSubField == RandomSubField.High && (Environment.TickCount64 / 500) % 2 == 0);
                        y = cursorY;
                    }
                    else
                        _circleSpreadLowValueRect = _circleSpreadHighValueRect = default;
                }
                else
                    _circleSpreadValueRect = _circleSpreadRandomButtonRect = default;
            }
            else if (t.Pattern == SpawnPattern.Cone)
            {
                _circleRadiusValueRect = default;
                _circleSpreadValueRect = default;
                _fullCircleRect = default;
                _lineLengthValueRect = default;
                var coneRange = t.RandomRanges.TryGetValue("ConeSpreadAngle", out var cor) ? cor : null;
                bool coneRandomOn = coneRange?.UseRandom ?? false;
                string coneText = coneRandomOn ? $"{coneRange!.Low:0.##} .. {coneRange.High:0.##}" : (_focusedField == InspectorNumberField.ConeSpreadAngle ? _editText : t.ConeSpreadAngle.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                (_coneSpreadAngleValueRect, _coneSpreadAngleRandomButtonRect) = InspectorDrawer.DrawFloatRowWithRandomToggle(sb, pixel, sb.GraphicsDevice, x, y, w, "Spread Angle", coneText, coneRandomOn, ref cursorY, showCaret: _focusedField == InspectorNumberField.ConeSpreadAngle && _randomSubField == RandomSubField.None && (Environment.TickCount64 / 500) % 2 == 0);
                y = cursorY;
                if (coneRandomOn)
                {
                    string lowText = _focusedField == InspectorNumberField.ConeSpreadAngle && _randomSubField == RandomSubField.Low ? _editText : coneRange!.Low.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    string highText = _focusedField == InspectorNumberField.ConeSpreadAngle && _randomSubField == RandomSubField.High ? _editText : coneRange!.High.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    _coneSpreadAngleLowValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Low", lowText, ref cursorY, showCaret: _focusedField == InspectorNumberField.ConeSpreadAngle && _randomSubField == RandomSubField.Low && (Environment.TickCount64 / 500) % 2 == 0);
                    y = cursorY;
                    _coneSpreadAngleHighValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "High", highText, ref cursorY, showCaret: _focusedField == InspectorNumberField.ConeSpreadAngle && _randomSubField == RandomSubField.High && (Environment.TickCount64 / 500) % 2 == 0);
                    y = cursorY;
                }
                else
                    _coneSpreadAngleLowValueRect = _coneSpreadAngleHighValueRect = default;
            }
            else if (t.Pattern == SpawnPattern.Line)
            {
                _circleRadiusValueRect = default;
                _circleSpreadValueRect = default;
                _fullCircleRect = default;
                _coneSpreadAngleValueRect = default;
                var lineRange = t.RandomRanges.TryGetValue("LineLength", out var lrr) ? lrr : null;
                bool lineRandomOn = lineRange?.UseRandom ?? false;
                string lineText = lineRandomOn ? $"{lineRange!.Low:0.##} .. {lineRange.High:0.##}" : (_focusedField == InspectorNumberField.LineLength ? _editText : t.LineLength.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                (_lineLengthValueRect, _lineLengthRandomButtonRect) = InspectorDrawer.DrawFloatRowWithRandomToggle(sb, pixel, sb.GraphicsDevice, x, y, w, "Length", lineText, lineRandomOn, ref cursorY, showCaret: _focusedField == InspectorNumberField.LineLength && _randomSubField == RandomSubField.None && (Environment.TickCount64 / 500) % 2 == 0);
                y = cursorY;
                if (lineRandomOn)
                {
                    string lowText = _focusedField == InspectorNumberField.LineLength && _randomSubField == RandomSubField.Low ? _editText : lineRange!.Low.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    string highText = _focusedField == InspectorNumberField.LineLength && _randomSubField == RandomSubField.High ? _editText : lineRange!.High.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    _lineLengthLowValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Low", lowText, ref cursorY, showCaret: _focusedField == InspectorNumberField.LineLength && _randomSubField == RandomSubField.Low && (Environment.TickCount64 / 500) % 2 == 0);
                    y = cursorY;
                    _lineLengthHighValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "High", highText, ref cursorY, showCaret: _focusedField == InspectorNumberField.LineLength && _randomSubField == RandomSubField.High && (Environment.TickCount64 / 500) % 2 == 0);
                    y = cursorY;
                }
                else
                    _lineLengthLowValueRect = _lineLengthHighValueRect = default;
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

        // Speed (only for Projectile), Lifetime (universal) with random toggles
        if (t.EntityKind == SpawnEntityKind.Projectile)
        {
            var speedRange = t.RandomRanges.TryGetValue("Speed", out var sr) ? sr : null;
            bool speedRandomOn = speedRange?.UseRandom ?? false;
            string speedText = speedRandomOn ? $"{speedRange!.Low:0.##} .. {speedRange.High:0.##}" : (_focusedField == InspectorNumberField.Speed ? _editText : t.Speed.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            bool speedCursorVisible = _focusedField == InspectorNumberField.Speed && _randomSubField == RandomSubField.None && (Environment.TickCount64 / 500) % 2 == 0;
            (_speedValueRect, _speedRandomButtonRect) = InspectorDrawer.DrawFloatRowWithRandomToggle(sb, pixel, sb.GraphicsDevice, x, y, w, "Speed", speedText, speedRandomOn, ref cursorY, showCaret: speedCursorVisible);
            y = cursorY;
            if (speedRandomOn)
            {
                string lowText = _focusedField == InspectorNumberField.Speed && _randomSubField == RandomSubField.Low ? _editText : speedRange!.Low.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                string highText = _focusedField == InspectorNumberField.Speed && _randomSubField == RandomSubField.High ? _editText : speedRange!.High.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                _speedLowValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Low", lowText, ref cursorY, showCaret: _focusedField == InspectorNumberField.Speed && _randomSubField == RandomSubField.Low && (Environment.TickCount64 / 500) % 2 == 0);
                y = cursorY;
                _speedHighValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "High", highText, ref cursorY, showCaret: _focusedField == InspectorNumberField.Speed && _randomSubField == RandomSubField.High && (Environment.TickCount64 / 500) % 2 == 0);
                y = cursorY;
            }
            else
                _speedLowValueRect = _speedHighValueRect = default;
        }
        else
        {
            _speedValueRect = default;
            _speedRandomButtonRect = default;
        }
        var lifetimeRange = t.RandomRanges.TryGetValue("Lifetime", out var lr) ? lr : null;
        bool lifetimeRandomOn = lifetimeRange?.UseRandom ?? false;
        string lifetimeText = lifetimeRandomOn ? $"{lifetimeRange!.Low:0.##} .. {lifetimeRange.High:0.##}" : (_focusedField == InspectorNumberField.Lifetime ? _editText : t.Lifetime.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
        bool lifetimeCursorVisible = _focusedField == InspectorNumberField.Lifetime && _randomSubField == RandomSubField.None && (Environment.TickCount64 / 500) % 2 == 0;
        (_lifetimeValueRect, _lifetimeRandomButtonRect) = InspectorDrawer.DrawFloatRowWithRandomToggle(sb, pixel, sb.GraphicsDevice, x, y, w, "Lifetime", lifetimeText, lifetimeRandomOn, ref cursorY, showCaret: lifetimeCursorVisible);
        y = cursorY;
        if (lifetimeRandomOn)
        {
            string lowText = _focusedField == InspectorNumberField.Lifetime && _randomSubField == RandomSubField.Low ? _editText : lifetimeRange!.Low.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            string highText = _focusedField == InspectorNumberField.Lifetime && _randomSubField == RandomSubField.High ? _editText : lifetimeRange!.High.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            _lifetimeLowValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Low", lowText, ref cursorY, showCaret: _focusedField == InspectorNumberField.Lifetime && _randomSubField == RandomSubField.Low && (Environment.TickCount64 / 500) % 2 == 0);
            y = cursorY;
            _lifetimeHighValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "High", highText, ref cursorY, showCaret: _focusedField == InspectorNumberField.Lifetime && _randomSubField == RandomSubField.High && (Environment.TickCount64 / 500) % 2 == 0);
            y = cursorY;
        }
        else
            _lifetimeLowValueRect = _lifetimeHighValueRect = default;

        // Direction pattern (projectile movement only)
        if (t.EntityKind == SpawnEntityKind.Projectile)
        {
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
                var ampRange = t.RandomRanges.TryGetValue("OscillationAmplitude", out var ar) ? ar : null;
                bool ampRandomOn = ampRange?.UseRandom ?? false;
                string ampText = ampRandomOn ? $"{ampRange!.Low:0.##} .. {ampRange.High:0.##}" : (_focusedField == InspectorNumberField.OscillationAmplitude ? _editText : t.OscillationAmplitude.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                (_oscillationAmplitudeValueRect, _oscillationAmplitudeRandomButtonRect) = InspectorDrawer.DrawFloatRowWithRandomToggle(sb, pixel, sb.GraphicsDevice, x, y, w, "Amplitude (°)", ampText, ampRandomOn, ref cursorY, showCaret: _focusedField == InspectorNumberField.OscillationAmplitude && _randomSubField == RandomSubField.None && (Environment.TickCount64 / 500) % 2 == 0);
                y = cursorY;
                if (ampRandomOn)
                {
                    string lowText = _focusedField == InspectorNumberField.OscillationAmplitude && _randomSubField == RandomSubField.Low ? _editText : ampRange!.Low.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    string highText = _focusedField == InspectorNumberField.OscillationAmplitude && _randomSubField == RandomSubField.High ? _editText : ampRange!.High.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    _oscillationAmplitudeLowValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Low", lowText, ref cursorY, showCaret: _focusedField == InspectorNumberField.OscillationAmplitude && _randomSubField == RandomSubField.Low && (Environment.TickCount64 / 500) % 2 == 0);
                    y = cursorY;
                    _oscillationAmplitudeHighValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "High", highText, ref cursorY, showCaret: _focusedField == InspectorNumberField.OscillationAmplitude && _randomSubField == RandomSubField.High && (Environment.TickCount64 / 500) % 2 == 0);
                    y = cursorY;
                }
                else
                    _oscillationAmplitudeLowValueRect = _oscillationAmplitudeHighValueRect = default;
                _orbitingDistanceValueRect = default;
            }
            else if (t.DirectionPattern == ProjectileDirectionPattern.Orbiting)
            {
                _oscillationAmplitudeValueRect = default;
                var distRange = t.RandomRanges.TryGetValue("OrbitingDistance", out var dr) ? dr : null;
                bool distRandomOn = distRange?.UseRandom ?? false;
                string distText = distRandomOn ? $"{distRange!.Low:0.##} .. {distRange.High:0.##}" : (_focusedField == InspectorNumberField.OrbitingDistance ? _editText : t.OrbitingDistance.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                (_orbitingDistanceValueRect, _orbitingDistanceRandomButtonRect) = InspectorDrawer.DrawFloatRowWithRandomToggle(sb, pixel, sb.GraphicsDevice, x, y, w, "Distance", distText, distRandomOn, ref cursorY, showCaret: _focusedField == InspectorNumberField.OrbitingDistance && _randomSubField == RandomSubField.None && (Environment.TickCount64 / 500) % 2 == 0);
                y = cursorY;
                if (distRandomOn)
                {
                    string lowText = _focusedField == InspectorNumberField.OrbitingDistance && _randomSubField == RandomSubField.Low ? _editText : distRange!.Low.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    string highText = _focusedField == InspectorNumberField.OrbitingDistance && _randomSubField == RandomSubField.High ? _editText : distRange!.High.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    _orbitingDistanceLowValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Low", lowText, ref cursorY, showCaret: _focusedField == InspectorNumberField.OrbitingDistance && _randomSubField == RandomSubField.Low && (Environment.TickCount64 / 500) % 2 == 0);
                    y = cursorY;
                    _orbitingDistanceHighValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, y, w, "High", highText, ref cursorY, showCaret: _focusedField == InspectorNumberField.OrbitingDistance && _randomSubField == RandomSubField.High && (Environment.TickCount64 / 500) % 2 == 0);
                    y = cursorY;
                }
                else
                    _orbitingDistanceLowValueRect = _orbitingDistanceHighValueRect = default;
            }
            else
            {
                _oscillationAmplitudeValueRect = default;
                _orbitingDistanceValueRect = default;
            }
        }
        else
        {
            _directionPatternValueRect = default;
            _directionPatternDropdownRect = default;
            _directionPatternOptionRects = Array.Empty<Rectangle>();
            _oscillationAmplitudeValueRect = default;
            _orbitingDistanceValueRect = default;
        }

        y = cursorY;
        InspectorDrawer.DrawSeparator(sb, pixel, x, y, w, ref cursorY);
        y = cursorY;
        _advancedFoldoutRect = InspectorDrawer.DrawFoldout(sb, pixel, sb.GraphicsDevice, x, y, w, "Advanced", _advancedExpanded, ref cursorY, canExpand: false);
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

    public void Update(IEventTrack track, InputManager input, Rectangle contentArea, EditorSelection? selection)
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
            else                 if (input.IsKeyPressed(Keys.Enter) || input.IsKeyPressed(Keys.Escape))
            {
                if (input.IsKeyPressed(Keys.Enter))
                    TryCommitField(t);
                _focusedField = InspectorNumberField.None;
                _randomSubField = RandomSubField.None;
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
                    _randomSubField = RandomSubField.None;
                    // Fall through to allow same click to hit foldout/dropdown
                }
                else
                {
                    // Click on a (possibly different) number field: commit current and focus the clicked one
                    GetNumberFieldAndSubFieldAt(pt, out var clicked, out var clickedSub);
                    if (clicked != _focusedField || clickedSub != _randomSubField)
                    {
                        TryCommitField(t);
                        _focusedField = clicked;
                        _randomSubField = clickedSub;
                        _editText = GetValueString(t, clicked, clickedSub);
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

        // Entity kind dropdown
        if (_entityKindDropdownOpen)
        {
            if (_entityKindDropdownRect.Contains(pt))
            {
                for (int i = 0; i < _entityKindOptionRects.Length; i++)
                {
                    if (_entityKindOptionRects[i].Contains(pt))
                    {
                        t.EntityKind = (SpawnEntityKind)i;
                        _entityKindDropdownOpen = false;
                        return;
                    }
                }
            }
            else
                _entityKindDropdownOpen = false;
            return;
        }

        // Click on random toggle button (before number field so button takes precedence)
        if (GetRandomButtonFieldAt(pt) is InspectorNumberField rndField)
        {
            ToggleRandomForField(t, rndField);
            _focusedField = InspectorNumberField.None;
            return;
        }

        // Click on a number field to start editing
        GetNumberFieldAndSubFieldAt(pt, out var fieldAt, out var subFieldAt);
        if (fieldAt != InspectorNumberField.None)
        {
            _focusedField = fieldAt;
            _randomSubField = subFieldAt;
            _editText = GetValueString(t, fieldAt, subFieldAt);
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
            // Advanced section is empty (canExpand: false); do not toggle
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

        if (!_entityKindDropdownOpen && _entityKindValueRect != default && _entityKindValueRect.Contains(pt))
        {
            _entityKindDropdownOpen = true;
            _positionDropdownOpen = false;
            _rotationDropdownOpen = false;
            _modeDropdownOpen = false;
            _patternDropdownOpen = false;
            _directionPatternDropdownOpen = false;
            return;
        }

        if (!_modeDropdownOpen && _modeValueRect.Contains(pt))
        {
            _modeDropdownOpen = true;
            _positionDropdownOpen = false;
            _rotationDropdownOpen = false;
            _patternDropdownOpen = false;
            _directionPatternDropdownOpen = false;
            _entityKindDropdownOpen = false;
            return;
        }

        if (t.SpawnMode == SpawnMode.Multiple && !_patternDropdownOpen && _patternValueRect != default && _patternValueRect.Contains(pt))
        {
            _patternDropdownOpen = true;
            _positionDropdownOpen = false;
            _rotationDropdownOpen = false;
            _modeDropdownOpen = false;
            _directionPatternDropdownOpen = false;
            _entityKindDropdownOpen = false;
            return;
        }

        if (t.EntityKind == SpawnEntityKind.Projectile && !_directionPatternDropdownOpen && _directionPatternValueRect != default && _directionPatternValueRect.Contains(pt))
        {
            _directionPatternDropdownOpen = true;
            _positionDropdownOpen = false;
            _rotationDropdownOpen = false;
            _modeDropdownOpen = false;
            _patternDropdownOpen = false;
            _entityKindDropdownOpen = false;
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
            _entityKindDropdownOpen = false;
            return;
        }

        Rectangle rotationModeRect = GetRotationModeValueRect(contentArea, t);
        if (_rotationExpanded && rotationModeRect.Contains(pt))
        {
            _rotationDropdownOpen = true;
            _positionDropdownOpen = false;
            _directionPatternDropdownOpen = false;
            _entityKindDropdownOpen = false;
        }
    }

    private bool IsPointInAnyNumberField(Point pt)
    {
        if (_speedValueRect.Contains(pt)) return true;
        if (_lifetimeValueRect.Contains(pt)) return true;
        if (_oscillationAmplitudeValueRect != default && _oscillationAmplitudeValueRect.Contains(pt)) return true;
        if (_orbitingDistanceValueRect != default && _orbitingDistanceValueRect.Contains(pt)) return true;
        if (_countValueRect != default && _countValueRect.Contains(pt)) return true;
        if (_circleRadiusValueRect != default && _circleRadiusValueRect.Contains(pt)) return true;
        if (_circleSpreadValueRect != default && _circleSpreadValueRect.Contains(pt)) return true;
        if (_coneSpreadAngleValueRect != default && _coneSpreadAngleValueRect.Contains(pt)) return true;
        if (_lineLengthValueRect != default && _lineLengthValueRect.Contains(pt)) return true;
        if (_speedLowValueRect != default && (_speedLowValueRect.Contains(pt) || _speedHighValueRect.Contains(pt))) return true;
        if (_lifetimeLowValueRect != default && (_lifetimeLowValueRect.Contains(pt) || _lifetimeHighValueRect.Contains(pt))) return true;
        if (_countLowValueRect != default && (_countLowValueRect.Contains(pt) || _countHighValueRect.Contains(pt))) return true;
        if (_circleRadiusLowValueRect != default && (_circleRadiusLowValueRect.Contains(pt) || _circleRadiusHighValueRect.Contains(pt))) return true;
        if (_circleSpreadLowValueRect != default && (_circleSpreadLowValueRect.Contains(pt) || _circleSpreadHighValueRect.Contains(pt))) return true;
        if (_coneSpreadAngleLowValueRect != default && (_coneSpreadAngleLowValueRect.Contains(pt) || _coneSpreadAngleHighValueRect.Contains(pt))) return true;
        if (_lineLengthLowValueRect != default && (_lineLengthLowValueRect.Contains(pt) || _lineLengthHighValueRect.Contains(pt))) return true;
        if (_oscillationAmplitudeLowValueRect != default && (_oscillationAmplitudeLowValueRect.Contains(pt) || _oscillationAmplitudeHighValueRect.Contains(pt))) return true;
        if (_orbitingDistanceLowValueRect != default && (_orbitingDistanceLowValueRect.Contains(pt) || _orbitingDistanceHighValueRect.Contains(pt))) return true;
        foreach (var r in _positionValueRects) if (r.Contains(pt)) return true;
        foreach (var r in _rotationValueRects) if (r.Contains(pt)) return true;
        return false;
    }

    private InspectorNumberField? GetRandomButtonFieldAt(Point pt)
    {
        if (_countRandomButtonRect != default && _countRandomButtonRect.Contains(pt)) return InspectorNumberField.Count;
        if (_speedRandomButtonRect != default && _speedRandomButtonRect.Contains(pt)) return InspectorNumberField.Speed;
        if (_lifetimeRandomButtonRect != default && _lifetimeRandomButtonRect.Contains(pt)) return InspectorNumberField.Lifetime;
        if (_circleRadiusRandomButtonRect != default && _circleRadiusRandomButtonRect.Contains(pt)) return InspectorNumberField.CircleRadius;
        if (_circleSpreadRandomButtonRect != default && _circleSpreadRandomButtonRect.Contains(pt)) return InspectorNumberField.CircleSpread;
        if (_coneSpreadAngleRandomButtonRect != default && _coneSpreadAngleRandomButtonRect.Contains(pt)) return InspectorNumberField.ConeSpreadAngle;
        if (_lineLengthRandomButtonRect != default && _lineLengthRandomButtonRect.Contains(pt)) return InspectorNumberField.LineLength;
        if (_oscillationAmplitudeRandomButtonRect != default && _oscillationAmplitudeRandomButtonRect.Contains(pt)) return InspectorNumberField.OscillationAmplitude;
        if (_orbitingDistanceRandomButtonRect != default && _orbitingDistanceRandomButtonRect.Contains(pt)) return InspectorNumberField.OrbitingDistance;
        return null;
    }

    private void ToggleRandomForField(SpawnEntityTrack t, InspectorNumberField field)
    {
        string key = GetRandomRangeKey(field);
        if (string.IsNullOrEmpty(key)) return;
        var range = t.GetOrAddRange(key, GetCurrentValueForField(t, field));
        range.UseRandom = !range.UseRandom;
        if (range.UseRandom)
        {
            range.Low = GetCurrentValueForField(t, field);
            range.High = range.Low;
        }
    }

    private static float GetCurrentValueForField(SpawnEntityTrack t, InspectorNumberField field)
    {
        return field switch
        {
            InspectorNumberField.Speed => t.Speed,
            InspectorNumberField.Lifetime => t.Lifetime,
            InspectorNumberField.Count => t.Count,
            InspectorNumberField.CircleRadius => t.CircleRadius,
            InspectorNumberField.CircleSpread => t.CircleSpread,
            InspectorNumberField.ConeSpreadAngle => t.ConeSpreadAngle,
            InspectorNumberField.LineLength => t.LineLength,
            InspectorNumberField.OscillationAmplitude => t.OscillationAmplitude,
            InspectorNumberField.OrbitingDistance => t.OrbitingDistance,
            _ => 0f
        };
    }

    private void GetNumberFieldAndSubFieldAt(Point pt, out InspectorNumberField field, out RandomSubField subField)
    {
        subField = RandomSubField.None;
        // Low/High rects first so we can set subField
        if (_countLowValueRect != default && _countLowValueRect.Contains(pt)) { field = InspectorNumberField.Count; subField = RandomSubField.Low; return; }
        if (_countHighValueRect != default && _countHighValueRect.Contains(pt)) { field = InspectorNumberField.Count; subField = RandomSubField.High; return; }
        if (_speedLowValueRect != default && _speedLowValueRect.Contains(pt)) { field = InspectorNumberField.Speed; subField = RandomSubField.Low; return; }
        if (_speedHighValueRect != default && _speedHighValueRect.Contains(pt)) { field = InspectorNumberField.Speed; subField = RandomSubField.High; return; }
        if (_lifetimeLowValueRect != default && _lifetimeLowValueRect.Contains(pt)) { field = InspectorNumberField.Lifetime; subField = RandomSubField.Low; return; }
        if (_lifetimeHighValueRect != default && _lifetimeHighValueRect.Contains(pt)) { field = InspectorNumberField.Lifetime; subField = RandomSubField.High; return; }
        if (_circleRadiusLowValueRect != default && _circleRadiusLowValueRect.Contains(pt)) { field = InspectorNumberField.CircleRadius; subField = RandomSubField.Low; return; }
        if (_circleRadiusHighValueRect != default && _circleRadiusHighValueRect.Contains(pt)) { field = InspectorNumberField.CircleRadius; subField = RandomSubField.High; return; }
        if (_circleSpreadLowValueRect != default && _circleSpreadLowValueRect.Contains(pt)) { field = InspectorNumberField.CircleSpread; subField = RandomSubField.Low; return; }
        if (_circleSpreadHighValueRect != default && _circleSpreadHighValueRect.Contains(pt)) { field = InspectorNumberField.CircleSpread; subField = RandomSubField.High; return; }
        if (_coneSpreadAngleLowValueRect != default && _coneSpreadAngleLowValueRect.Contains(pt)) { field = InspectorNumberField.ConeSpreadAngle; subField = RandomSubField.Low; return; }
        if (_coneSpreadAngleHighValueRect != default && _coneSpreadAngleHighValueRect.Contains(pt)) { field = InspectorNumberField.ConeSpreadAngle; subField = RandomSubField.High; return; }
        if (_lineLengthLowValueRect != default && _lineLengthLowValueRect.Contains(pt)) { field = InspectorNumberField.LineLength; subField = RandomSubField.Low; return; }
        if (_lineLengthHighValueRect != default && _lineLengthHighValueRect.Contains(pt)) { field = InspectorNumberField.LineLength; subField = RandomSubField.High; return; }
        if (_oscillationAmplitudeLowValueRect != default && _oscillationAmplitudeLowValueRect.Contains(pt)) { field = InspectorNumberField.OscillationAmplitude; subField = RandomSubField.Low; return; }
        if (_oscillationAmplitudeHighValueRect != default && _oscillationAmplitudeHighValueRect.Contains(pt)) { field = InspectorNumberField.OscillationAmplitude; subField = RandomSubField.High; return; }
        if (_orbitingDistanceLowValueRect != default && _orbitingDistanceLowValueRect.Contains(pt)) { field = InspectorNumberField.OrbitingDistance; subField = RandomSubField.Low; return; }
        if (_orbitingDistanceHighValueRect != default && _orbitingDistanceHighValueRect.Contains(pt)) { field = InspectorNumberField.OrbitingDistance; subField = RandomSubField.High; return; }
        // Main value rects
        if (_speedValueRect != default && _speedValueRect.Contains(pt)) { field = InspectorNumberField.Speed; return; }
        if (_lifetimeValueRect != default && _lifetimeValueRect.Contains(pt)) { field = InspectorNumberField.Lifetime; return; }
        if (_oscillationAmplitudeValueRect != default && _oscillationAmplitudeValueRect.Contains(pt)) { field = InspectorNumberField.OscillationAmplitude; return; }
        if (_orbitingDistanceValueRect != default && _orbitingDistanceValueRect.Contains(pt)) { field = InspectorNumberField.OrbitingDistance; return; }
        if (_countValueRect != default && _countValueRect.Contains(pt)) { field = InspectorNumberField.Count; return; }
        if (_circleRadiusValueRect != default && _circleRadiusValueRect.Contains(pt)) { field = InspectorNumberField.CircleRadius; return; }
        if (_circleSpreadValueRect != default && _circleSpreadValueRect.Contains(pt)) { field = InspectorNumberField.CircleSpread; return; }
        if (_coneSpreadAngleValueRect != default && _coneSpreadAngleValueRect.Contains(pt)) { field = InspectorNumberField.ConeSpreadAngle; return; }
        if (_lineLengthValueRect != default && _lineLengthValueRect.Contains(pt)) { field = InspectorNumberField.LineLength; return; }
        if (_positionValueRects.Length >= 3)
        {
            if (_positionValueRects[0].Contains(pt)) { field = InspectorNumberField.PositionX; return; }
            if (_positionValueRects[1].Contains(pt)) { field = InspectorNumberField.PositionY; return; }
            if (_positionValueRects[2].Contains(pt)) { field = InspectorNumberField.PositionZ; return; }
        }
        if (_rotationValueRects.Length >= 3)
        {
            if (_rotationValueRects[0].Contains(pt)) { field = InspectorNumberField.RotationX; return; }
            if (_rotationValueRects[1].Contains(pt)) { field = InspectorNumberField.RotationY; return; }
            if (_rotationValueRects[2].Contains(pt)) { field = InspectorNumberField.RotationZ; return; }
        }
        field = InspectorNumberField.None;
    }

    private InspectorNumberField GetNumberFieldAt(Point pt)
    {
        GetNumberFieldAndSubFieldAt(pt, out var f, out _);
        return f;
    }

    private static string GetValueString(SpawnEntityTrack t, InspectorNumberField field, RandomSubField subField)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (field == InspectorNumberField.Count && subField != RandomSubField.None && t.RandomRanges.TryGetValue("Count", out var cr))
            return subField == RandomSubField.Low ? ((int)Math.Round(cr.Low)).ToString(inv) : ((int)Math.Round(cr.High)).ToString(inv);
        if (subField == RandomSubField.Low && t.RandomRanges.TryGetValue(GetRandomRangeKey(field), out var rangeLow))
            return rangeLow.Low.ToString("0.##", inv);
        if (subField == RandomSubField.High && t.RandomRanges.TryGetValue(GetRandomRangeKey(field), out var rangeHigh))
            return rangeHigh.High.ToString("0.##", inv);
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
        string key = GetRandomRangeKey(_focusedField);
        if (_randomSubField == RandomSubField.Low || _randomSubField == RandomSubField.High)
        {
            if (!float.TryParse(_editText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float value))
                return;
            var range = t.GetOrAddRange(key, GetCurrentValueForField(t, _focusedField));
            if (_randomSubField == RandomSubField.Low)
                range.Low = _focusedField == InspectorNumberField.Count ? Math.Clamp(value, 1f, 10f) : value;
            else
                range.High = _focusedField == InspectorNumberField.Count ? Math.Clamp(value, 1f, 10f) : value;
            return;
        }
        if (!float.TryParse(_editText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float mainValue))
            return;
        switch (_focusedField)
        {
            case InspectorNumberField.Speed:
                t.Speed = Math.Max(0.001f, mainValue);
                break;
            case InspectorNumberField.Lifetime:
                t.Lifetime = Math.Max(0.001f, mainValue);
                break;
            case InspectorNumberField.Count:
                t.Count = (int)Math.Clamp(Math.Round(mainValue), 1, 10);
                break;
            case InspectorNumberField.CircleRadius:
                t.CircleRadius = Math.Max(0f, mainValue);
                break;
            case InspectorNumberField.CircleSpread:
                t.CircleSpread = Math.Clamp(mainValue, 1f, 360f);
                break;
            case InspectorNumberField.ConeSpreadAngle:
                t.ConeSpreadAngle = Math.Clamp(mainValue, 0.1f, 360f);
                break;
            case InspectorNumberField.LineLength:
                t.LineLength = Math.Max(0.001f, mainValue);
                break;
            case InspectorNumberField.OscillationAmplitude:
                t.OscillationAmplitude = Math.Clamp(mainValue, 0.1f, 180f);
                break;
            case InspectorNumberField.OrbitingDistance:
                t.OrbitingDistance = Math.Max(0.001f, mainValue);
                break;
            case InspectorNumberField.PositionX:
                if (t.PositionMode == PositionMode.Absolute) t.PositionAbsolute = new Vector3(mainValue, t.PositionAbsolute.Y, t.PositionAbsolute.Z);
                else t.PositionRelative = new Vector3(mainValue, t.PositionRelative.Y, t.PositionRelative.Z);
                break;
            case InspectorNumberField.PositionY:
                if (t.PositionMode == PositionMode.Absolute) t.PositionAbsolute = new Vector3(t.PositionAbsolute.X, mainValue, t.PositionAbsolute.Z);
                else t.PositionRelative = new Vector3(t.PositionRelative.X, mainValue, t.PositionRelative.Z);
                break;
            case InspectorNumberField.PositionZ:
                if (t.PositionMode == PositionMode.Absolute) t.PositionAbsolute = new Vector3(t.PositionAbsolute.X, t.PositionAbsolute.Y, mainValue);
                else t.PositionRelative = new Vector3(t.PositionRelative.X, t.PositionRelative.Y, mainValue);
                break;
            case InspectorNumberField.RotationX:
                t.RotationEuler = new Vector3(mainValue, t.RotationEuler.Y, t.RotationEuler.Z);
                break;
            case InspectorNumberField.RotationY:
                t.RotationEuler = new Vector3(t.RotationEuler.X, mainValue, t.RotationEuler.Z);
                break;
            case InspectorNumberField.RotationZ:
                t.RotationEuler = new Vector3(t.RotationEuler.X, t.RotationEuler.Y, mainValue);
                break;
        }
    }
}
