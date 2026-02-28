using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Input;
using AGX_Beat_Sync.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.Editor;

public class SfxInspectorRenderer : IInspectorRenderer
{
    private bool _pathFocused;
    private string _editText = "";
    private Rectangle _pathValueRect;

    public void Draw(SpriteBatch sb, Rectangle contentArea, IEventTrack track, InputManager input, ref int cursorY, EditorSelection? selection)
    {
        if (track is not SfxTrack t)
            return;

        var pixel = PanelBase.GetPixelTexture(sb.GraphicsDevice);
        int x = contentArea.X + InspectorDrawer.Padding;
        int y = contentArea.Y + InspectorDrawer.Padding;
        int w = contentArea.Width - InspectorDrawer.Padding * 2;

        string valueText = _pathFocused ? _editText : t.FmodAudioEventPath;
        bool cursorVisible = _pathFocused && (Environment.TickCount64 / 500) % 2 == 0;
        _pathValueRect = InspectorDrawer.DrawStringRow(sb, pixel, sb.GraphicsDevice, x, y, w, "FMOD Audio Event Path", valueText, ref cursorY, showCaret: cursorVisible);
    }

    public void Update(IEventTrack track, InputManager input, Rectangle contentArea, EditorSelection? selection)
    {
        if (track is not SfxTrack t)
            return;

        if (_pathFocused)
        {
            if (input.IsKeyPressed(Keys.Back))
            {
                if (_editText.Length > 0)
                    _editText = _editText[..^1];
            }
            else if (input.IsKeyPressed(Keys.Enter) || input.IsKeyPressed(Keys.Escape))
            {
                if (input.IsKeyPressed(Keys.Enter))
                    t.FmodAudioEventPath = _editText;
                _pathFocused = false;
                return;
            }
            else
            {
                char? c = TryGetPathChar(input);
                if (c.HasValue)
                {
                    _editText += c.Value;
                }
            }

            if (input.MouseLeftPressed && !_pathValueRect.Contains(input.MousePosition))
            {
                t.FmodAudioEventPath = _editText;
                _pathFocused = false;
            }
            return;
        }

        if (input.MouseLeftPressed && contentArea.Contains(input.MousePosition) && _pathValueRect.Contains(input.MousePosition))
        {
            _pathFocused = true;
            _editText = t.FmodAudioEventPath ?? "";
        }
    }

    private static char? TryGetPathChar(InputManager input)
    {
        bool shift = input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift);
        foreach (Keys key in Enum.GetValues<Keys>())
        {
            if (!input.IsKeyPressed(key)) continue;
            char? c = key switch
            {
                Keys.Space => ' ',
                Keys.OemMinus => shift ? '_' : '-',
                Keys.OemPeriod => '.',
                Keys.OemComma => ',',
                Keys.OemQuestion => shift ? '?' : '/',
                Keys.OemBackslash => shift ? '|' : '\\',
                Keys.OemPipe => '|',
                Keys.OemCloseBrackets => shift ? '}' : ']',
                Keys.OemOpenBrackets => shift ? '{' : '[',
                Keys.OemSemicolon => shift ? ':' : ';',
                Keys.OemQuotes => shift ? '"' : '\'',
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
                Keys.A => (char)(shift ? 'A' : 'a'),
                Keys.B => (char)(shift ? 'B' : 'b'),
                Keys.C => (char)(shift ? 'C' : 'c'),
                Keys.D => (char)(shift ? 'D' : 'd'),
                Keys.E => (char)(shift ? 'E' : 'e'),
                Keys.F => (char)(shift ? 'F' : 'f'),
                Keys.G => (char)(shift ? 'G' : 'g'),
                Keys.H => (char)(shift ? 'H' : 'h'),
                Keys.I => (char)(shift ? 'I' : 'i'),
                Keys.J => (char)(shift ? 'J' : 'j'),
                Keys.K => (char)(shift ? 'K' : 'k'),
                Keys.L => (char)(shift ? 'L' : 'l'),
                Keys.M => (char)(shift ? 'M' : 'm'),
                Keys.N => (char)(shift ? 'N' : 'n'),
                Keys.O => (char)(shift ? 'O' : 'o'),
                Keys.P => (char)(shift ? 'P' : 'p'),
                Keys.Q => (char)(shift ? 'Q' : 'q'),
                Keys.R => (char)(shift ? 'R' : 'r'),
                Keys.S => (char)(shift ? 'S' : 's'),
                Keys.T => (char)(shift ? 'T' : 't'),
                Keys.U => (char)(shift ? 'U' : 'u'),
                Keys.V => (char)(shift ? 'V' : 'v'),
                Keys.W => (char)(shift ? 'W' : 'w'),
                Keys.X => (char)(shift ? 'X' : 'x'),
                Keys.Y => (char)(shift ? 'Y' : 'y'),
                Keys.Z => (char)(shift ? 'Z' : 'z'),
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
                _ => null
            };
            if (c.HasValue) return c;
        }
        return null;
    }
}
