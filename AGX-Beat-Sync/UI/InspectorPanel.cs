using System.Linq;
using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Editor;
using AGX_Beat_Sync.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.UI;

public class InspectorPanel : PanelBase
{
    private const int ScrollbarWidth = 12;
    private const int MinScrollbarThumbHeight = 24;
    private const int InspectorHeaderHeight = 22;

    private int _scrollY;
    private int _contentHeight;
    private bool _scrollbarThumbDragging;
    private IEventTrack? _lastSelectedTrack;
    private RenderTarget2D? _contentRenderTarget;
    private static readonly RasterizerState ScissorRasterizer = new() { ScissorTestEnable = true, CullMode = CullMode.None };

    public EditorSelection? Selection { get; set; }
    public InputManager? Input { get; set; }
    public Project? Project { get; set; }

    private const int TrackTypeRowHeight = 22;
    private bool _trackTypeDropdownOpen;
    private Rectangle _trackTypeValueRect;
    private Rectangle[] _trackTypeOptionRects = Array.Empty<Rectangle>();

    public InspectorPanel()
    {
        Title = "Inspector";
        BackgroundColor = new Color(38, 40, 44);
    }

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
        }

        int scrollableHeight = contentArea.Height - InspectorHeaderHeight - TrackTypeRowHeight;
        int trackTypeRowY = contentArea.Y + InspectorHeaderHeight;

        // Track type dropdown at top of inspector
        if (Project != null && Selection.SelectedEventTrack is EventTrackBase currentTrack)
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
                            if (newTypeId != currentTrack.TrackTypeId)
                            {
                                var newTrack = EventTrackRegistry.CreateTrack(newTypeId);
                                if (newTrack is EventTrackBase newBase)
                                {
                                    newBase.Order = currentTrack.Order;
                                    newBase.DisplayName = currentTrack.DisplayName;
                                    newBase.TrackColor = currentTrack.TrackColor;
                                    newBase.EventTimes = new List<double>(currentTrack.EventTimes);
                                    newBase.EventDurations = new Dictionary<double, double>(currentTrack.EventDurations);
                                }
                                int idx = Project.EventTracks.IndexOf(currentTrack);
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
                int maxScroll = Math.Max(0, _contentHeight - scrollableHeight);
                if (scrollbar.Height > MinScrollbarThumbHeight && maxScroll > 0)
                {
                    int thumbHeight = Math.Max(MinScrollbarThumbHeight, (int)(scrollableHeight / (double)_contentHeight * scrollbar.Height));
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
                int maxScroll = Math.Max(0, _contentHeight - scrollableHeight);
                _scrollY -= Input.ScrollWheelDelta;
                _scrollY = Math.Clamp(_scrollY, 0, maxScroll);
            }

            // Start scrollbar thumb drag on click
            if (Input.MouseLeftPressed)
            {
                var thumb = GetScrollbarThumbBounds(content, scrollableHeight);
                if (GetScrollbarBounds(content).Contains(Input.MousePosition))
                {
                    if (thumb.Contains(Input.MousePosition))
                        _scrollbarThumbDragging = true;
                    else
                    {
                        // Click on track: jump to position
                        int maxScroll = Math.Max(0, _contentHeight - scrollableHeight);
                        var scrollbar = GetScrollbarBounds(content);
                        int thumbHeight = Math.Max(MinScrollbarThumbHeight, (int)(scrollableHeight / (double)_contentHeight * scrollbar.Height));
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

            // Pass visible content area for hit-test (below track type row)
            var scrollableY = contentArea.Y + InspectorHeaderHeight + TrackTypeRowHeight - _scrollY;
            var visibleArea = new Rectangle(contentArea.X, scrollableY, contentArea.Width, scrollableHeight);
            if (visibleArea.Contains(Input.MousePosition))
                renderer.Update(Selection.SelectedEventTrack, Input, visibleArea);
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
                var scrollableRect = new Rectangle(contentArea.X, contentArea.Y + InspectorHeaderHeight + TrackTypeRowHeight, contentArea.Width, contentArea.Height - InspectorHeaderHeight - TrackTypeRowHeight);
                int contentTop = contentArea.Y + InspectorHeaderHeight + TrackTypeRowHeight - _scrollY;
                var shiftedArea = new Rectangle(contentArea.X, contentTop, contentArea.Width, scrollableRect.Height + _scrollY);

                int sw = Math.Max(1, scrollableRect.Width);
                int sh = Math.Max(1, scrollableRect.Height);
                if (sw > 0 && sh > 0)
                {
                    spriteBatch.End();
                    var gd = spriteBatch.GraphicsDevice;
                    int backBufferW = gd.PresentationParameters.BackBufferWidth;
                    int backBufferH = gd.PresentationParameters.BackBufferHeight;

                    if (_contentRenderTarget == null || _contentRenderTarget.Width != sw || _contentRenderTarget.Height != sh)
                    {
                        _contentRenderTarget?.Dispose();
                        _contentRenderTarget = new RenderTarget2D(gd, sw, sh, false, SurfaceFormat.Color, DepthFormat.None);
                    }
                    gd.SetRenderTarget(_contentRenderTarget);
                    gd.Viewport = new Viewport(0, 0, sw, sh);
                    gd.ScissorRectangle = new Rectangle(0, 0, sw, sh);
                    gd.Clear(new Color(38, 40, 44));
                    var transform = Matrix.CreateTranslation(-contentArea.X, -(contentArea.Y + InspectorHeaderHeight + TrackTypeRowHeight), 0f);
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, ScissorRasterizer, null, transform);
                    int cursorY = contentTop;
                    renderer.Draw(spriteBatch, shiftedArea, Selection.SelectedEventTrack, Input, ref cursorY);
                    _contentHeight = Math.Max(0, cursorY - (contentArea.Y + InspectorHeaderHeight + TrackTypeRowHeight));
                    spriteBatch.End();

                    gd.SetRenderTarget(null);
                    gd.Viewport = new Viewport(0, 0, backBufferW, backBufferH);
                    gd.ScissorRectangle = new Rectangle(0, 0, backBufferW, backBufferH);

                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, RasterizerState.CullNone);
                    DrawPanelBackground(spriteBatch);
                    string headerTitle = Selection.SelectedEventTime.HasValue ? "Inspector: Note" : "Inspector: Track";
                    int headerCursorY = contentArea.Y;
                    InspectorDrawer.DrawHeader(spriteBatch, pixel, gd, contentArea.X, contentArea.Y, contentArea.Width, headerTitle, ref headerCursorY);

                    // Track type row at top of inspector content
                    int trackTypeY = contentArea.Y + InspectorHeaderHeight;
                    var types = EventTrackRegistry.AllTypes;
                    var currentDesc = types.FirstOrDefault(d => d.TrackTypeId == Selection.SelectedEventTrack.TrackTypeId);
                    string typeDisplay = currentDesc?.DisplayName ?? Selection.SelectedEventTrack.TrackTypeId;
                    int trackTypeCursorY = trackTypeY;
                    _trackTypeValueRect = InspectorDrawer.DrawEnumRow(spriteBatch, pixel, gd, contentArea.X + InspectorDrawer.Padding, trackTypeY, contentArea.Width - InspectorDrawer.Padding * 2, "Track Type", typeDisplay, ref trackTypeCursorY);
                    if (!_trackTypeDropdownOpen)
                        _trackTypeOptionRects = Array.Empty<Rectangle>();

                    spriteBatch.Draw(_contentRenderTarget, scrollableRect, new Rectangle(0, 0, sw, sh), Color.White);

                    // Draw dropdown on top of scrollable content so it isn't covered
                    if (_trackTypeDropdownOpen && types.Count > 0)
                    {
                        int selectedIdx = 0;
                        for (int i = 0; i < types.Count; i++)
                        {
                            if (types[i].TrackTypeId == Selection.SelectedEventTrack.TrackTypeId) { selectedIdx = i; break; }
                        }
                        var optionNames = types.Select(t => t.DisplayName).ToArray();
                        (_, _trackTypeOptionRects) = InspectorDrawer.DrawDropdownList(spriteBatch, pixel, gd, contentArea.X + InspectorDrawer.Padding, trackTypeCursorY, contentArea.Width - InspectorDrawer.Padding * 2, optionNames, selectedIdx, ref trackTypeCursorY, Input.MousePosition);
                    }
                }
                else
                {
                    _contentHeight = 0;
                    // Zero-sized content: still avoid first flush for panel; do End/Begin then draw bg+header
                    spriteBatch.End();
                    var gd = spriteBatch.GraphicsDevice;
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, RasterizerState.CullNone);
                    DrawPanelBackground(spriteBatch);
                    string headerTitle = Selection.SelectedEventTime.HasValue ? "Inspector: Note" : "Inspector: Track";
                    int cursorY = contentArea.Y;
                    InspectorDrawer.DrawHeader(spriteBatch, pixel, gd, contentArea.X, contentArea.Y, contentArea.Width, headerTitle, ref cursorY);
                }

                // Clamp scroll to updated content height
                int maxScroll = Math.Max(0, _contentHeight - scrollableRect.Height);
                _scrollY = Math.Clamp(_scrollY, 0, maxScroll);

                // Draw scrollbar when content overflows (scrollable region only)
                if (_contentHeight > scrollableRect.Height)
                {
                    var scrollbar = GetScrollbarBounds(content);
                    spriteBatch.Draw(pixel, scrollbar, new Color(45, 48, 55));
                    var thumb = GetScrollbarThumbBounds(content, scrollableRect.Height);
                    spriteBatch.Draw(pixel, thumb, new Color(90, 95, 105));
                }
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

    private Rectangle GetScrollbarThumbBounds(Rectangle content, int? visibleHeight = null)
    {
        var contentArea = new Rectangle(content.X, content.Y, Math.Max(0, content.Width - ScrollbarWidth), content.Height);
        int visible = visibleHeight ?? contentArea.Height;
        var scrollbar = GetScrollbarBounds(content);
        if (_contentHeight <= visible || scrollbar.Height <= 0)
            return scrollbar;
        int thumbHeight = Math.Max(MinScrollbarThumbHeight, (int)(visible / (double)_contentHeight * scrollbar.Height));
        int maxScroll = Math.Max(0, _contentHeight - visible);
        int thumbY = scrollbar.Y + (maxScroll > 0 ? (int)(_scrollY / (double)maxScroll * (scrollbar.Height - thumbHeight)) : 0);
        return new Rectangle(scrollbar.X + 2, thumbY, scrollbar.Width - 4, thumbHeight);
    }

    protected override void DrawContent(SpriteBatch spriteBatch)
    {
        // Empty when we have a renderer (Draw override handles it); used when no selection
    }
}
