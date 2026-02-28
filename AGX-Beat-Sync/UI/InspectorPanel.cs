using System.Linq;
using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Editor;
using AGX_Beat_Sync.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.UI;

public class InspectorPanel : PanelBase
{
    private const int ScrollbarWidth = 12;
    private const int MinScrollbarThumbHeight = 24;
    private const int InspectorHeaderHeight = 22;

    private int _scrollY;
    private int _contentHeight; // height of renderer body only (below fixed header/name/type)
    private bool _scrollbarThumbDragging;
    private IEventTrack? _lastSelectedTrack;
    private static readonly RasterizerState ScissorRasterizer = new() { ScissorTestEnable = true, CullMode = CullMode.None };

    public EditorSelection? Selection { get; set; }
    public InputManager? Input { get; set; }
    public Project? Project { get; set; }

    private const int NameRowHeight = 20;
    private const int TrackTypeRowHeight = 22;
    private bool _trackTypeDropdownOpen;
    private Rectangle _trackTypeValueRect;
    private Rectangle[] _trackTypeOptionRects = Array.Empty<Rectangle>();

    private bool _nameFocused;
    private string _nameEditText = "";
    private Rectangle _nameValueRect;

    public InspectorPanel()
    {
        Title = "Inspector";
        BackgroundColor = new Color(38, 40, 44);
    }

    /// <summary>Content starts below the timeline strips band so it aligns with the timeline panel.</summary>
    public override Rectangle ContentBounds =>
        new(Bounds.X, Bounds.Y + PanelLayout.TimelineStripsHeight, Bounds.Width, Math.Max(0, Bounds.Height - PanelLayout.TimelineStripsHeight));

    public override string? GetHoverText(Point mouse)
    {
        if (!ContainsPoint(mouse)) return null;
        if (Selection?.SelectedEventTrack != null)
            return $"Inspector — {Selection.SelectedEventTrack.DisplayName}";
        return "Inspector";
    }

    public override void Update(GameTime gameTime)
    {
        if (Selection?.SelectedEventTrack == null || Input == null)
            return;

        var content = ContentBounds;
        var contentArea = new Rectangle(content.X, content.Y, Math.Max(0, content.Width - ScrollbarWidth), content.Height);
        var renderer = InspectorRendererRegistry.Get(Selection.SelectedEventTrack.TrackTypeId);

        if (renderer == null)
        {
            _scrollY = 0;
            _contentHeight = 0;
            _lastSelectedTrack = null;
            return;
        }

        // Reset scroll when selection changes
        if (_lastSelectedTrack != Selection.SelectedEventTrack)
        {
            _scrollY = 0;
            _contentHeight = 0;
            _lastSelectedTrack = Selection.SelectedEventTrack;
            _trackTypeDropdownOpen = false;
            _nameFocused = false;
        }

        int fixedTopHeight = InspectorHeaderHeight + NameRowHeight + TrackTypeRowHeight;
        int totalContentHeight = fixedTopHeight + _contentHeight;
        int scrollableHeight = contentArea.Height; // entire content area scrolls
        int maxScroll = Math.Max(0, totalContentHeight - scrollableHeight);
        int nameRowY = contentArea.Y + InspectorHeaderHeight - _scrollY;
        int trackTypeRowY = contentArea.Y + InspectorHeaderHeight + NameRowHeight - _scrollY;

        // Name field: focus and edit (must run before track type so we don't steal clicks)
        if (Selection.SelectedEventTrack is EventTrackBase currentTrack)
        {
            if (_nameFocused)
            {
                if (Input.IsKeyPressed(Keys.Back))
                {
                    if (_nameEditText.Length > 0)
                        _nameEditText = _nameEditText[..^1];
                }
                else if (Input.IsKeyPressed(Keys.Enter) || Input.IsKeyPressed(Keys.Escape))
                {
                    if (Input.IsKeyPressed(Keys.Enter))
                        currentTrack.DisplayName = string.IsNullOrWhiteSpace(_nameEditText) ? "Event Track" : _nameEditText.Trim();
                    _nameFocused = false;
                    return;
                }
                else
                {
                    char? c = TryGetPrintableChar(Input);
                    if (c.HasValue && _nameEditText.Length < 64)
                        _nameEditText += c.Value;
                }
                if (Input.MouseLeftPressed && !_nameValueRect.Contains(Input.MousePosition))
                {
                    currentTrack.DisplayName = string.IsNullOrWhiteSpace(_nameEditText) ? "Event Track" : _nameEditText.Trim();
                    _nameFocused = false;
                }
                return;
            }
            if (Input.MouseLeftPressed && _nameValueRect.Contains(Input.MousePosition))
            {
                _nameFocused = true;
                _nameEditText = currentTrack.DisplayName ?? "";
                return;
            }
        }

        // Track type dropdown at top of inspector
        if (Project != null && Selection.SelectedEventTrack is EventTrackBase selectedTrack)
        {
            if (Input.MouseLeftPressed)
            {
                if (_trackTypeDropdownOpen && _trackTypeOptionRects.Length > 0)
                {
                    var types = EventTrackRegistry.AllTypes;
                    for (int i = 0; i < _trackTypeOptionRects.Length && i < types.Count; i++)
                    {
                        if (_trackTypeOptionRects[i].Contains(Input.MousePosition))
                        {
                            string newTypeId = types[i].TrackTypeId;
                            if (newTypeId != selectedTrack.TrackTypeId)
                            {
                                var newTrack = EventTrackRegistry.CreateTrack(newTypeId);
                                if (newTrack is EventTrackBase newBase)
                                {
                                    newBase.Order = selectedTrack.Order;
                                    newBase.DisplayName = types[i].DisplayName;
                                    newBase.TrackColor = selectedTrack.TrackColor;
                                    newBase.EventTimes = new List<double>(selectedTrack.EventTimes);
                                    newBase.EventDurations = new Dictionary<double, double>(selectedTrack.EventDurations);
                                    if (newTrack is ChangeEntityColorTrack newColorTrack && selectedTrack is ChangeEntityColorTrack oldColorTrack)
                                        newColorTrack.EventColors = new Dictionary<double, EntityColor>(oldColorTrack.EventColors);
                                }
                                int idx = Project.EventTracks.IndexOf(selectedTrack);
                                if (idx >= 0)
                                {
                                    Project.EventTracks.RemoveAt(idx);
                                    Project.EventTracks.Insert(idx, (EventTrackBase)newTrack);
                                }
                                else
                                    Project.EventTracks.Add((EventTrackBase)newTrack);
                                Selection.SelectedEventTrack = newTrack;
                                _lastSelectedTrack = newTrack;
                            }
                            _trackTypeDropdownOpen = false;
                            return;
                        }
                    }
                    _trackTypeDropdownOpen = false;
                }
                else if (_trackTypeValueRect.Contains(Input.MousePosition))
                {
                    _trackTypeDropdownOpen = !_trackTypeDropdownOpen;
                    return;
                }
                else
                    _trackTypeDropdownOpen = false;
            }
        }

        // Scrollbar thumb drag (persists while mouse is down even if outside panel)
        if (_scrollbarThumbDragging)
        {
            if (Input.MouseLeftDown)
            {
                var scrollbar = GetScrollbarBounds(content);
                if (scrollbar.Height > MinScrollbarThumbHeight && maxScroll > 0)
                {
                    int thumbHeight = Math.Max(MinScrollbarThumbHeight, (int)(scrollableHeight / (double)totalContentHeight * scrollbar.Height));
                    int travel = scrollbar.Height - thumbHeight;
                    int thumbY = Input.MousePosition.Y - thumbHeight / 2;
                    thumbY = Math.Clamp(thumbY, scrollbar.Y, scrollbar.Bottom - thumbHeight);
                    _scrollY = travel > 0 ? (int)((thumbY - scrollbar.Y) / (double)travel * maxScroll) : 0;
                    _scrollY = Math.Clamp(_scrollY, 0, maxScroll);
                }
            }
            else
                _scrollbarThumbDragging = false;
        }
        else if (ContainsPoint(Input.MousePosition))
        {
            // Mouse wheel scroll
            if (Input.ScrollWheelDelta != 0 && contentArea.Contains(Input.MousePosition))
            {
                _scrollY -= Input.ScrollWheelDelta;
                _scrollY = Math.Clamp(_scrollY, 0, maxScroll);
            }

            // Start scrollbar thumb drag on click
            if (Input.MouseLeftPressed)
            {
                var thumb = GetScrollbarThumbBounds(content, scrollableHeight, totalContentHeight);
                if (GetScrollbarBounds(content).Contains(Input.MousePosition))
                {
                    if (thumb.Contains(Input.MousePosition))
                        _scrollbarThumbDragging = true;
                    else
                    {
                        var scrollbar = GetScrollbarBounds(content);
                        int thumbHeight = Math.Max(MinScrollbarThumbHeight, (int)(scrollableHeight / (double)totalContentHeight * scrollbar.Height));
                        int travel = scrollbar.Height - thumbHeight;
                        if (travel > 0)
                        {
                            int thumbY = Input.MousePosition.Y - scrollbar.Y - thumbHeight / 2;
                            _scrollY = (int)(thumbY / (double)travel * maxScroll);
                            _scrollY = Math.Clamp(_scrollY, 0, maxScroll);
                        }
                    }
                }
            }

            // Pass content area for hit-test (renderer controls are in screen space)
            if (contentArea.Contains(Input.MousePosition))
                renderer.Update(Selection.SelectedEventTrack, Input, contentArea, Selection);
        }
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        var content = ContentBounds;
        var contentArea = new Rectangle(content.X, content.Y, Math.Max(0, content.Width - ScrollbarWidth), content.Height);
        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);

        if (Selection?.SelectedEventTrack != null && Input != null)
        {
            var renderer = InspectorRendererRegistry.Get(Selection.SelectedEventTrack.TrackTypeId);
            if (renderer != null)
            {
                int fixedTop = InspectorHeaderHeight + NameRowHeight + TrackTypeRowHeight;

                spriteBatch.End();
                var gd = spriteBatch.GraphicsDevice;
                int backBufferW = gd.PresentationParameters.BackBufferWidth;
                int backBufferH = gd.PresentationParameters.BackBufferHeight;
                gd.ScissorRectangle = content;

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, ScissorRasterizer, null, Matrix.Identity);
                DrawPanelBackground(spriteBatch);

                // Header, name, track type (all scroll with _scrollY)
                string headerTitle = Selection.SelectedEventTime.HasValue ? "Inspector: Note" : "Inspector: Track";
                int headerY = contentArea.Y - _scrollY;
                int headerCursorY = headerY;
                InspectorDrawer.DrawHeader(spriteBatch, pixel, gd, contentArea.X, headerY, contentArea.Width, headerTitle, ref headerCursorY);

                int nameRowY = contentArea.Y + InspectorHeaderHeight - _scrollY;
                int nameRowCursorY = nameRowY;
                string nameDisplay = _nameFocused ? _nameEditText : (Selection.SelectedEventTrack.DisplayName ?? "");
                bool nameCaretVisible = _nameFocused && (Environment.TickCount64 / 500) % 2 == 0;
                _nameValueRect = InspectorDrawer.DrawStringRow(spriteBatch, pixel, gd, contentArea.X + InspectorDrawer.Padding, nameRowY, contentArea.Width - InspectorDrawer.Padding * 2, "Name", nameDisplay, ref nameRowCursorY, showCaret: nameCaretVisible);

                int trackTypeY = contentArea.Y + InspectorHeaderHeight + NameRowHeight - _scrollY;
                var types = EventTrackRegistry.AllTypes;
                var currentDesc = types.FirstOrDefault(d => d.TrackTypeId == Selection.SelectedEventTrack.TrackTypeId);
                string typeDisplay = currentDesc?.DisplayName ?? Selection.SelectedEventTrack.TrackTypeId;
                int trackTypeCursorY = trackTypeY;
                _trackTypeValueRect = InspectorDrawer.DrawEnumRow(spriteBatch, pixel, gd, contentArea.X + InspectorDrawer.Padding, trackTypeY, contentArea.Width - InspectorDrawer.Padding * 2, "Track Type", typeDisplay, ref trackTypeCursorY);
                if (!_trackTypeDropdownOpen)
                    _trackTypeOptionRects = Array.Empty<Rectangle>();

                // Renderer body (starts at fixedTop - _scrollY)
                int bodyStartY = contentArea.Y + fixedTop - _scrollY;
                var bodyArea = new Rectangle(contentArea.X, bodyStartY, contentArea.Width, Math.Max(contentArea.Height, 4096));
                int cursorY = bodyStartY;
                renderer.Draw(spriteBatch, bodyArea, Selection.SelectedEventTrack, Input, ref cursorY, Selection);
                _contentHeight = Math.Max(0, cursorY - (contentArea.Y + fixedTop));

                spriteBatch.End();
                gd.ScissorRectangle = new Rectangle(0, 0, backBufferW, backBufferH);

                // Dropdown on top (drawn in screen space, same scroll)
                if (_trackTypeDropdownOpen && types.Count > 0)
                {
                    int selectedIdx = 0;
                    for (int i = 0; i < types.Count; i++)
                    {
                        if (types[i].TrackTypeId == Selection.SelectedEventTrack.TrackTypeId) { selectedIdx = i; break; }
                    }
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, RasterizerState.CullNone);
                    var optionNames = types.Select(t => t.DisplayName).ToArray();
                    (_, _trackTypeOptionRects) = InspectorDrawer.DrawDropdownList(spriteBatch, pixel, gd, contentArea.X + InspectorDrawer.Padding, trackTypeCursorY, contentArea.Width - InspectorDrawer.Padding * 2, optionNames, selectedIdx, ref trackTypeCursorY, Input.MousePosition);
                    spriteBatch.End();
                }

                // Clamp scroll to updated total content height
                int totalContentHeight = fixedTop + _contentHeight;
                int maxScroll = Math.Max(0, totalContentHeight - contentArea.Height);
                _scrollY = Math.Clamp(_scrollY, 0, maxScroll);

                // Scrollbar when content overflows
                if (totalContentHeight > contentArea.Height)
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, RasterizerState.CullNone);
                    var scrollbar = GetScrollbarBounds(content);
                    spriteBatch.Draw(pixel, scrollbar, new Color(45, 48, 55));
                    var thumb = GetScrollbarThumbBounds(content, contentArea.Height, totalContentHeight);
                    spriteBatch.Draw(pixel, thumb, new Color(90, 95, 105));
                    if (thumb.Height >= 10)
                    {
                        var gripColor = new Color(45, 50, 58);
                        int gripLeft = thumb.X + 2;
                        int gripW = Math.Max(1, thumb.Width - 4);
                        int centerY = thumb.Y + thumb.Height / 2;
                        spriteBatch.Draw(pixel, new Rectangle(gripLeft, centerY - 2, gripW, 1), gripColor);
                        spriteBatch.Draw(pixel, new Rectangle(gripLeft, centerY + 2, gripW, 1), gripColor);
                    }
                    spriteBatch.End();
                }

                // Leave batch active for subsequent panels
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, RasterizerState.CullNone);
            }
            else
            {
                DrawPanelBackground(spriteBatch);
                DrawContent(spriteBatch);
            }
        }
        else
        {
            DrawPanelBackground(spriteBatch);
            DrawContent(spriteBatch);
        }
    }

    private static Rectangle GetScrollbarBounds(Rectangle content)
    {
        return new Rectangle(content.Right - ScrollbarWidth, content.Y, ScrollbarWidth, content.Height);
    }

    private Rectangle GetScrollbarThumbBounds(Rectangle content, int visibleHeight, int totalContentHeight)
    {
        var scrollbar = GetScrollbarBounds(content);
        if (totalContentHeight <= visibleHeight || scrollbar.Height <= 0)
            return new Rectangle(scrollbar.X, scrollbar.Y, scrollbar.Width, Math.Min(scrollbar.Height, MinScrollbarThumbHeight));
        int thumbHeight = Math.Max(MinScrollbarThumbHeight, (int)(visibleHeight / (double)totalContentHeight * scrollbar.Height));
        int maxScroll = Math.Max(0, totalContentHeight - visibleHeight);
        int thumbY = scrollbar.Y + (maxScroll > 0 ? (int)(_scrollY / (double)maxScroll * (scrollbar.Height - thumbHeight)) : 0);
        return new Rectangle(scrollbar.X + 2, thumbY, scrollbar.Width - 4, thumbHeight);
    }

    protected override void DrawContent(SpriteBatch spriteBatch)
    {
        // Empty when we have a renderer (Draw override handles it); used when no selection
    }

    private static char? TryGetPrintableChar(InputManager input)
    {
        bool shift = input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift);
        foreach (Keys key in Enum.GetValues<Keys>())
        {
            if (!input.IsKeyPressed(key)) continue;
            char? c = key switch
            {
                Keys.Space => ' ',
                Keys.OemMinus => shift ? '_' : '-',
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
