using System.Linq;
using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Input;
using AGX_Beat_Sync.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.Editor;

public class ChangeWeatherInspectorRenderer : IInspectorRenderer
{
    private bool _weatherDropdownOpen;
    private Rectangle _weatherValueRect;
    private Rectangle _weatherDropdownRect;
    private Rectangle[] _weatherOptionRects = Array.Empty<Rectangle>();

    private static readonly string[] WeatherOptions = Enum.GetNames<WeatherKind>();

    public void Draw(SpriteBatch sb, Rectangle contentArea, IEventTrack track, InputManager input, ref int cursorY, EditorSelection? selection)
    {
        if (track is not ChangeWeatherTrack t)
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
        WeatherKind displayWeather = WeatherKind.Sunny;
        if (hasNoteSelection && selection!.SelectedNotes.Count > 0)
        {
            var first = selection.SelectedNotes.First(n => n.Track == t);
            displayWeather = t.GetWeather(first.EventTime);
        }

        string weatherText = displayWeather.ToString();
        _weatherValueRect = InspectorDrawer.DrawEnumRow(sb, pixel, sb.GraphicsDevice, x, y, w, "Weather", weatherText, ref cursorY);
        y = cursorY;
        if (_weatherDropdownOpen)
        {
            int selected = (int)displayWeather;
            (_weatherDropdownRect, _weatherOptionRects) = InspectorDrawer.DrawDropdownList(sb, pixel, sb.GraphicsDevice, x, y, w, WeatherOptions, selected, ref cursorY, input.MousePosition);
        }
        else
            _weatherOptionRects = Array.Empty<Rectangle>();

        if (!hasNoteSelection)
        {
            InspectorDrawer.DrawRowLabel(sb, pixel, sb.GraphicsDevice, x, y, w, "Select a note to set weather", ref cursorY);
        }
    }

    public void Update(IEventTrack track, InputManager input, Rectangle contentArea, EditorSelection? selection)
    {
        if (track is not ChangeWeatherTrack t)
            return;

        var pt = input.MousePosition;
        if (!contentArea.Contains(pt) || selection?.SelectedNotes == null)
        {
            if (!input.MouseLeftPressed)
                return;
            _weatherDropdownOpen = false;
            return;
        }

        bool hasNoteSelection = selection.SelectedNotes.Count > 0 && selection.SelectedNotes.Any(n => n.Track == t);
        if (!hasNoteSelection)
        {
            if (input.MouseLeftPressed)
                _weatherDropdownOpen = false;
            return;
        }

        if (input.MouseLeftPressed)
        {
            if (_weatherDropdownOpen && _weatherOptionRects.Length > 0)
            {
                for (int i = 0; i < _weatherOptionRects.Length; i++)
                {
                    if (_weatherOptionRects[i].Contains(pt))
                    {
                        var newWeather = (WeatherKind)i;
                        foreach (var (noteTrack, eventTime) in selection.SelectedNotes)
                        {
                            if (noteTrack == t)
                                t.SetWeather(eventTime, newWeather);
                        }
                        _weatherDropdownOpen = false;
                        return;
                    }
                }
                _weatherDropdownOpen = false;
                return;
            }

            if (_weatherValueRect.Contains(pt))
            {
                _weatherDropdownOpen = true;
                return;
            }
            _weatherDropdownOpen = false;
        }
    }
}
