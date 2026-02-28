using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Input;
using AGX_Beat_Sync.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.Editor;

public class ScreenshakeInspectorRenderer : IInspectorRenderer
{
    private bool _amplitudeFocused;
    private bool _durationFocused;
    private string _editText = "";
    private Rectangle _amplitudeValueRect;
    private Rectangle _durationValueRect;

    public void Draw(SpriteBatch sb, Rectangle contentArea, IEventTrack track, InputManager input, ref int cursorY, EditorSelection? selection)
    {
        if (track is not ScreenshakeTrack t)
            return;

        var pixel = PanelBase.GetPixelTexture(sb.GraphicsDevice);
        int x = contentArea.X + InspectorDrawer.Padding;
        int y = contentArea.Y + InspectorDrawer.Padding;
        int w = contentArea.Width - InspectorDrawer.Padding * 2;

        InspectorDrawer.DrawHeader(sb, pixel, sb.GraphicsDevice, x, y, w, t.DisplayName, ref cursorY);
        y = cursorY;
        InspectorDrawer.DrawSeparator(sb, pixel, x, y, w, ref cursorY);
        y = cursorY;

        string amplitudeText = _amplitudeFocused ? _editText : t.Amplitude.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        bool amplitudeCaret = _amplitudeFocused && (Environment.TickCount64 / 500) % 2 == 0;
        _amplitudeValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, cursorY, w, "Amplitude", amplitudeText, ref cursorY, showCaret: amplitudeCaret);

        string durationText = _durationFocused ? _editText : t.Duration.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        bool durationCaret = _durationFocused && (Environment.TickCount64 / 500) % 2 == 0;
        _durationValueRect = InspectorDrawer.DrawFloatRow(sb, pixel, sb.GraphicsDevice, x, cursorY, w, "Duration", durationText, ref cursorY, showCaret: durationCaret);
    }

    public void Update(IEventTrack track, InputManager input, Rectangle contentArea, EditorSelection? selection)
    {
        if (track is not ScreenshakeTrack t)
            return;

        if (_amplitudeFocused)
        {
            if (input.IsKeyPressed(Keys.Back))
            {
                if (_editText.Length > 0)
                    _editText = _editText[..^1];
            }
            else if (input.IsKeyPressed(Keys.Enter) || input.IsKeyPressed(Keys.Escape))
            {
                if (input.IsKeyPressed(Keys.Enter) && float.TryParse(_editText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                    t.Amplitude = Math.Max(0f, v);
                _amplitudeFocused = false;
                return;
            }
            else
            {
                char? c = TryGetFloatChar(input);
                if (c.HasValue)
                    _editText += c.Value;
            }

            if (input.MouseLeftPressed && !_amplitudeValueRect.Contains(input.MousePosition))
            {
                if (float.TryParse(_editText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                    t.Amplitude = Math.Max(0f, v);
                _amplitudeFocused = false;
            }
            return;
        }

        if (_durationFocused)
        {
            if (input.IsKeyPressed(Keys.Back))
            {
                if (_editText.Length > 0)
                    _editText = _editText[..^1];
            }
            else if (input.IsKeyPressed(Keys.Enter) || input.IsKeyPressed(Keys.Escape))
            {
                if (input.IsKeyPressed(Keys.Enter) && float.TryParse(_editText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                    t.Duration = Math.Max(0f, v);
                _durationFocused = false;
                return;
            }
            else
            {
                char? c = TryGetFloatChar(input);
                if (c.HasValue)
                    _editText += c.Value;
            }

            if (input.MouseLeftPressed && !_durationValueRect.Contains(input.MousePosition))
            {
                if (float.TryParse(_editText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                    t.Duration = Math.Max(0f, v);
                _durationFocused = false;
            }
            return;
        }

        if (input.MouseLeftPressed && contentArea.Contains(input.MousePosition))
        {
            if (_amplitudeValueRect.Contains(input.MousePosition))
            {
                _amplitudeFocused = true;
                _editText = t.Amplitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (_durationValueRect.Contains(input.MousePosition))
            {
                _durationFocused = true;
                _editText = t.Duration.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }

    private static char? TryGetFloatChar(InputManager input)
    {
        bool shift = input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift);
        foreach (Keys key in Enum.GetValues<Keys>())
        {
            if (!input.IsKeyPressed(key)) continue;
            char? c = key switch
            {
                Keys.OemMinus => '-',
                Keys.OemPeriod => '.',
                Keys.D0 => shift ? ')' : '0',
                Keys.D1 => shift ? '!' : '1',
                Keys.D2 => shift ? '@' : '2',
                Keys.D3 => shift ? '#' : '3',
                Keys.D4 => shift ? '$' : '4',
                Keys.D5 => shift ? '%' : '5',
                Keys.D6 => shift ? '^' : '6',
                Keys.D7 => shift ? '&' : '7',
                Keys.D8 => shift ? '*' : '8',
                Keys.D9 => shift ? '(' : '9',
                Keys.NumPad0 => '0',
                Keys.NumPad1 => '1',
                Keys.NumPad2 => '2',
                Keys.NumPad3 => '3',
                Keys.NumPad4 => '4',
                Keys.NumPad5 => '5',
                Keys.NumPad6 => '6',
                Keys.NumPad7 => '7',
                Keys.NumPad8 => '8',
                Keys.NumPad9 => '9',
                Keys.Decimal => '.',
                _ => null
            };
            if (c.HasValue) return c;
        }
        return null;
    }
}
