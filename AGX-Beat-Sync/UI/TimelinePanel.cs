using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using AGX_Beat_Sync.Audio;
using AGX_Beat_Sync.Commands;
using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Editor;
using AGX_Beat_Sync.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync.UI;

public class TimelinePanel : PanelBase
{
    private const int LaneHeight = 28;
    private const int DefaultRowCount = 4;
    private const double EventBlockDisplayDuration = 0.25; // seconds, width of one event block
    private const int HorizontalScrollbarHeight = 14;
    private const int ScrollbarWidth = 12;
    private const int MinScrollbarThumbHeight = 24;
    private const double MinTotalTimeRange = 60.0;
    /// <summary>FL Studio-style piano key strip width on the left.</summary>
    private const int PianoStripWidth = 44;

    /// <summary>First visible row index for vertical scroll (each row = one event track).</summary>
    private int _laneScrollOffset;
    /// <summary>Horizontal scrollbar: dragging the time thumb.</summary>
    private bool _horizontalScrollDragging;
    private int _horizontalScrollDragStartPixel;
    private double _horizontalScrollDragStartTime;
    /// <summary>Vertical scrollbar: dragging the lane thumb.</summary>
    private bool _verticalScrollDragging;
    private int _verticalScrollDragStartY;
    private int _verticalScrollDragStartOffset;
    /// <summary>Dragging the playhead bulb to seek.</summary>
    private bool _playheadBulbDragging;
    /// <summary>Right-drag seeking: playhead follows mouse while right button is held.</summary>
    private bool _rightDragSeeking;
    /// <summary>Middle-drag pan: ViewStartTime when drag started so we apply total DragDelta once (grab-to-pan).</summary>
    private double _panDragStartViewTime;
    /// <summary>Dragging note edge to shorten/lengthen (Ableton/FL style).</summary>
    private EventTrackBase? _durationResizeTrack;
    private double _durationResizeEventTime;
    /// <summary>True = dragging left edge (move start); false = dragging right edge (move end).</summary>
    private bool _durationResizeFromLeft;
    /// <summary>Dragging note body to move note to new time/track.</summary>
    private EventTrackBase? _noteMoveTrack;
    private double _noteMoveEventTime;
    private const int NoteResizeHandleWidth = 8;
    private const double MinNoteDurationSeconds = 0.01;

    public Project? Project { get; set; }
    public Transport? Transport { get; set; }
    /// <summary>Time used to draw the playhead (smoothed when playing). If null, Transport.CurrentTime is used.</summary>
    public double PlayheadDisplayTime { get; set; }
    public TimelineViewState? ViewState { get; set; }
    public Input.InputManager? Input { get; set; }
    public EditorSelection? Selection { get; set; }
    public WaveformCache? Waveform { get; set; }
    public CommandStack? CommandStack { get; set; }
    /// <summary>When set, called on seek (e.g. right-click) so the host can sync transport and audio.</summary>
    public Action<double>? SeekRequested { get; set; }
    /// <summary>When set, clicks inside this rect are ignored (e.g. game view divider overlapping timeline edge).</summary>
    public Rectangle? IgnoreClickRect { get; set; }

    /// <summary>FL Studio-style dark background for the piano roll area.</summary>
    private static readonly Color PianoRollBackground = new(28, 30, 34);
    /// <summary>Piano strip: lighter row (like white key).</summary>
    private static readonly Color PianoStripLight = new(52, 55, 62);
    /// <summary>Piano strip: darker row (like black key).</summary>
    private static readonly Color PianoStripDark = new(38, 40, 46);
    /// <summary>Note gradient top (subtle highlight).</summary>
    private static readonly Color NoteFillTop = new(238, 145, 100);
    /// <summary>Note gradient bottom (subtle shadow).</summary>
    private static readonly Color NoteFillBottom = new(218, 115, 75);
    /// <summary>Note border for definition.</summary>
    private static readonly Color NoteBorder = new(150, 75, 45);
    /// <summary>Selected note gradient top.</summary>
    private static readonly Color NoteSelectedFillTop = new(248, 178, 132);
    /// <summary>Selected note gradient bottom.</summary>
    private static readonly Color NoteSelectedFillBottom = new(238, 158, 112);
    /// <summary>Selected note border.</summary>
    private static readonly Color NoteSelectedBorder = new(210, 125, 80);
    /// <summary>Resize handle on the right edge of notes (visual cue for drag-to-resize).</summary>
    private static readonly Color NoteResizeHandle = new(110, 55, 30);

    private const int NoteTextureWidth = 64;
    private const int NoteTextureHeight = 24;
    private const int NoteCornerRadius = 4;
    private static Texture2D? s_noteFillTexture;
    private static Texture2D? s_noteSelectedFillTexture;
    private static Texture2D? s_noteBorderTexture;
    private static Texture2D? s_noteSelectedBorderTexture;
    private static GraphicsDevice? s_noteTextureDevice;

    public TimelinePanel()
    {
        Title = "Timeline";
        BackgroundColor = PianoRollBackground;
    }

    private static bool IsInsideRoundedRect(int px, int py, int w, int h, int r)
    {
        if (r <= 0) return px >= 0 && px < w && py >= 0 && py < h;
        int r2 = r * r;
        int cxTL = r, cyTL = r;
        int cxTR = w - 1 - r, cyTR = r;
        int cxBL = r, cyBL = h - 1 - r;
        int cxBR = w - 1 - r, cyBR = h - 1 - r;
        if (px >= r && px < w - r && py >= r && py < h - r) return true;
        if (px < r && py < r && (px - cxTL) * (px - cxTL) + (py - cyTL) * (py - cyTL) <= r2) return true;
        if (px >= w - r && py < r && (px - cxTR) * (px - cxTR) + (py - cyTR) * (py - cyTR) <= r2) return true;
        if (px < r && py >= h - r && (px - cxBL) * (px - cxBL) + (py - cyBL) * (py - cyBL) <= r2) return true;
        if (px >= w - r && py >= h - r && (px - cxBR) * (px - cxBR) + (py - cyBR) * (py - cyBR) <= r2) return true;
        if (px < r && py >= r && py < h - r) return true;
        if (px >= w - r && py >= r && py < h - r) return true;
        if (py < r && px >= r && px < w - r) return true;
        if (py >= h - r && px >= r && px < w - r) return true;
        return false;
    }

    private static Texture2D CreateRoundedRectGradient(GraphicsDevice gd, int w, int h, int r, Color top, Color bottom)
    {
        var data = new Color[w * h];
        for (int py = 0; py < h; py++)
        {
            float t = h <= 1 ? 1f : (float)py / (h - 1);
            Color c = new Color(
                (byte)(top.R + (bottom.R - top.R) * t),
                (byte)(top.G + (bottom.G - top.G) * t),
                (byte)(top.B + (bottom.B - top.B) * t),
                (byte)255);
            for (int px = 0; px < w; px++)
                data[py * w + px] = IsInsideRoundedRect(px, py, w, h, r) ? c : Color.Transparent;
        }
        var tex = new Texture2D(gd, w, h);
        tex.SetData(data);
        return tex;
    }

    private static Texture2D CreateRoundedRectSolid(GraphicsDevice gd, int w, int h, int r, Color fill)
    {
        var data = new Color[w * h];
        for (int py = 0; py < h; py++)
            for (int px = 0; px < w; px++)
                data[py * w + px] = IsInsideRoundedRect(px, py, w, h, r) ? fill : Color.Transparent;
        var tex = new Texture2D(gd, w, h);
        tex.SetData(data);
        return tex;
    }

    private void EnsureNoteTextures(GraphicsDevice gd)
    {
        if (s_noteTextureDevice != gd)
        {
            s_noteFillTexture?.Dispose();
            s_noteSelectedFillTexture?.Dispose();
            s_noteBorderTexture?.Dispose();
            s_noteSelectedBorderTexture?.Dispose();
            s_noteTextureDevice = gd;
            s_noteFillTexture = null;
            s_noteSelectedFillTexture = null;
            s_noteBorderTexture = null;
            s_noteSelectedBorderTexture = null;
        }
        if (s_noteFillTexture == null)
        {
            s_noteFillTexture = CreateRoundedRectGradient(gd, NoteTextureWidth, NoteTextureHeight, NoteCornerRadius, NoteFillTop, NoteFillBottom);
            s_noteSelectedFillTexture = CreateRoundedRectGradient(gd, NoteTextureWidth, NoteTextureHeight, NoteCornerRadius, NoteSelectedFillTop, NoteSelectedFillBottom);
            s_noteBorderTexture = CreateRoundedRectSolid(gd, NoteTextureWidth, NoteTextureHeight, NoteCornerRadius, NoteBorder);
            s_noteSelectedBorderTexture = CreateRoundedRectSolid(gd, NoteTextureWidth, NoteTextureHeight, NoteCornerRadius, NoteSelectedBorder);
        }
    }

    public override string? GetHoverText(Point mouse)
    {
        var content = ContentBounds;
        var trackArea = GetTrackContentBounds(content);

        // Playhead bulb (head is above content — allow hit-test there)
        int bulbCenterY = content.Y - TimelinePlayheadRenderer.PlayheadHeadOffset;
        int r = TimelinePlayheadRenderer.PlayheadBulbRadius;
        if (ViewState != null && Transport != null)
        {
            double t = double.IsFinite(PlayheadDisplayTime) ? PlayheadDisplayTime : Transport.CurrentTime;
            float playheadX = ViewState.TimeToScreen(t, trackArea.X);
            float dx = mouse.X - playheadX;
            float dy = mouse.Y - bulbCenterY;
            if (dx * dx + dy * dy <= r * r)
                return "Playhead — drag to seek (Shift: snap to grid)";
            if (GetPlayheadStripBounds(content).Contains(mouse))
                return "Click to move playhead";
        }

        if (!ContentBounds.Contains(mouse)) return null;

        var pianoStrip = GetPianoStripBounds(content);
        if (pianoStrip.Contains(mouse))
        {
            int row = (mouse.Y - pianoStrip.Y) / LaneHeight;
            if (Project?.EventTracks != null && row >= 0 && row < Project.EventTracks.Count)
                return $"Track: {Project.EventTracks[row].DisplayName}";
            return "Timeline";
        }

        if (GetScrollbarBounds(content).Contains(mouse)) return "Vertical scroll";
        if (GetHorizontalScrollbarBounds(content).Contains(mouse)) return "Horizontal scroll";

        if (trackArea.Contains(mouse) && ViewState != null)
        {
            var (hitTrack, hitTime, hitLeftEdge, hitRightEdge) = HitTestEvent(content, mouse.X, mouse.Y);
            if (hitTrack != null && hitTime.HasValue)
                return hitLeftEdge ? $"{hitTrack.DisplayName} • drag to move start (Shift: free-form)" : hitRightEdge ? $"{hitTrack.DisplayName} • drag to shorten/lengthen (Shift: free-form)" : $"{hitTrack.DisplayName} • {TimeFormatHelper.Format(hitTime.Value)} — drag to move, right-click to delete";
            if (hitTrack != null)
            {
                double time = Math.Max(0, ViewState.ScreenToTime(mouse.X, trackArea.X));
                return $"{hitTrack.DisplayName} • {TimeFormatHelper.Format(time)} — click to add event";
            }
        }

        return "Timeline";
    }

    public override MouseCursor? GetDesiredCursor(Point mouse)
    {
        if (!ContentBounds.Contains(mouse) || ViewState == null) return null;
        if (_durationResizeTrack != null) return MouseCursor.SizeWE;
        var content = ContentBounds;
        var trackArea = GetTrackContentBounds(content);
        if (!trackArea.Contains(mouse)) return null;
        var (_, _, hitLeftEdge, hitRightEdge) = HitTestEvent(content, mouse.X, mouse.Y);
        return (hitLeftEdge || hitRightEdge) ? MouseCursor.SizeWE : null;
    }

    /// <summary>Keep playhead in view: pan view when current time scrolls off left or right.</summary>
    private void FollowPlayheadIfOutOfView()
    {
        if (ViewState == null || Transport == null) return;
        var content = ContentBounds;
        var trackArea = GetTrackContentBounds(content);
        double viewEnd = ViewState.ViewEndTime(trackArea.Width);
        double t = Transport.CurrentTime;

        const double marginSeconds = 0.5;
        const double playheadPositionFromLeft = 0.15; // keep playhead at ~15% from left when following

        if (t < ViewState.ViewStartTime)
            ViewState.ViewStartTime = Math.Max(0, t - marginSeconds);
        else if (t > viewEnd)
            ViewState.ViewStartTime = Math.Max(0, t - trackArea.Width / (double)ViewState.Zoom * (1.0 - playheadPositionFromLeft));
        ClampViewToTimeRange(content);
    }

    public override void Update(GameTime gameTime)
    {
        if (ViewState == null || Input == null)
            return;

        if (Transport != null && Transport.IsPlaying)
            FollowPlayheadIfOutOfView();

        var content = ContentBounds;

        // End horizontal/vertical scroll, playhead bulb drag, note resize, and note move on left release (anywhere)
        if (Input.MouseLeftReleased)
        {
            _horizontalScrollDragging = false;
            _verticalScrollDragging = false;
            _playheadBulbDragging = false;
            _durationResizeTrack = null;
            _noteMoveTrack = null;
        }
        if (Input.MouseRightReleased)
            _rightDragSeeking = false;

        // Update playhead position while dragging the bulb (snap by default, Shift = smooth)
        if (_playheadBulbDragging && Input.MouseLeftDown && ViewState != null && Transport != null)
        {
            var trackArea = GetTrackContentBounds(content);
            double time = Math.Max(0, ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X));
            bool smooth = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
            if (!smooth)
                time = Transport.SnapToBeat(time, Math.Max(1, ViewState.GridSubdivisionsPerBeat));
            if (SeekRequested != null)
                SeekRequested(time);
            else
                Transport.Seek(time);
        }

        // Right-drag: playhead follows mouse (snap by default, Shift = smooth)
        if (_rightDragSeeking && Input.MouseRightDown && ViewState != null && Transport != null)
        {
            var trackArea = GetTrackContentBounds(content);
            double time = Math.Max(0, ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X));
            bool smooth = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
            if (!smooth)
                time = Transport.SnapToBeat(time, Math.Max(1, ViewState.GridSubdivisionsPerBeat));
            if (SeekRequested != null)
                SeekRequested(time);
            else
                Transport.Seek(time);
        }

        // Note resize: drag left edge (move start) or right edge (move end). Snap to grid by default; Shift = free-form.
        if (_durationResizeTrack != null && Input.MouseLeftDown && ViewState != null && Transport != null)
        {
            var trackArea = GetTrackContentBounds(content);
            bool freeForm = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
            if (_durationResizeFromLeft)
            {
                double endTime = _durationResizeEventTime + _durationResizeTrack.GetDuration(_durationResizeEventTime);
                double newStartTime = Math.Max(0, Math.Min(endTime - MinNoteDurationSeconds, ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X)));
                if (!freeForm)
                    newStartTime = Transport.SnapToBeat(newStartTime, Math.Max(1, ViewState.GridSubdivisionsPerBeat));
                newStartTime = Math.Max(0, Math.Min(endTime - MinNoteDurationSeconds, newStartTime));
                double newDuration = endTime - newStartTime;
                if (newDuration >= MinNoteDurationSeconds && Math.Abs(newStartTime - _durationResizeEventTime) > 0.0001)
                {
                    _durationResizeTrack.EventTimes.Remove(_durationResizeEventTime);
                    _durationResizeTrack.EventDurations.Remove(_durationResizeEventTime);
                    _durationResizeTrack.EventTimes.Add(newStartTime);
                    _durationResizeTrack.SetDuration(newStartTime, newDuration);
                    _durationResizeTrack.EventTimes.Sort();
                    _durationResizeEventTime = newStartTime;
                    if (Selection != null)
                    {
                        Selection.SelectedEventTrack = _durationResizeTrack;
                        Selection.SelectedEventTime = newStartTime;
                    }
                }
            }
            else
            {
                double endTime = Math.Max(_durationResizeEventTime + MinNoteDurationSeconds, ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X));
                double duration = endTime - _durationResizeEventTime;
                if (!freeForm)
                {
                    double beatDuration = 60.0 / Transport.BPM / Math.Max(1, ViewState.GridSubdivisionsPerBeat);
                    duration = Math.Max(MinNoteDurationSeconds, Math.Round(duration / beatDuration) * beatDuration);
                }
                _durationResizeTrack.SetDuration(_durationResizeEventTime, duration);
            }
        }

        // Note move: drag note body to new time and/or track. Snap to grid by default; Shift = free-form.
        if (_noteMoveTrack != null && Input.MouseLeftDown && Input.IsDragging && ViewState != null && Transport != null && Project?.EventTracks != null)
        {
            var trackArea = GetTrackContentBounds(content);
            double newTime = Math.Max(0, ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X));
            bool freeForm = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
            if (!freeForm)
                newTime = Transport.SnapToBeat(newTime, Math.Max(1, ViewState.GridSubdivisionsPerBeat));
            newTime = Math.Max(0, newTime);
            int row = _laneScrollOffset + (Input.MousePosition.Y - trackArea.Y) / LaneHeight;
            row = Math.Clamp(row, 0, Project.EventTracks.Count - 1);
            var newTrack = Project.EventTracks[row] as EventTrackBase;
            bool positionChanged = Math.Abs(newTime - _noteMoveEventTime) > 0.0001 || newTrack != _noteMoveTrack;
            bool slotFree = newTrack == _noteMoveTrack || !newTrack!.EventTimes.Contains(newTime);
            if (newTrack != null && positionChanged && slotFree)
            {
                double duration = _noteMoveTrack.GetDuration(_noteMoveEventTime);
                _noteMoveTrack.EventTimes.Remove(_noteMoveEventTime);
                _noteMoveTrack.EventDurations.Remove(_noteMoveEventTime);
                newTrack.EventTimes.Add(newTime);
                newTrack.SetDuration(newTime, duration);
                newTrack.EventTimes.Sort();
                _noteMoveEventTime = newTime;
                _noteMoveTrack = newTrack;
                if (Selection != null)
                {
                    Selection.SelectedEventTrack = newTrack;
                    Selection.SelectedEventTime = newTime;
                }
            }
        }

        // Vertical scrollbar drag (runs even if mouse leaves panel)
        if (_verticalScrollDragging && Input.MouseLeftDown)
        {
            var scrollbar = GetScrollbarBounds(content);
            var trackArea = GetTrackContentBounds(content);
            int totalLanes = GetTotalLaneCount();
            int visibleLanes = GetVisibleLaneCount(trackArea);
            if (totalLanes > visibleLanes && scrollbar.Height > 0)
            {
                int thumbHeight = Math.Max(MinScrollbarThumbHeight, (int)(visibleLanes / (double)totalLanes * scrollbar.Height));
                int travel = scrollbar.Height - thumbHeight;
                if (travel > 0)
                {
                    int deltaPixel = Input.MousePosition.Y - _verticalScrollDragStartY;
                    double range = totalLanes - visibleLanes;
                    int deltaOffset = (int)Math.Round(deltaPixel * range / travel);
                    int newOffset = Math.Clamp(_verticalScrollDragStartOffset + deltaOffset, 0, (int)range);
                    _laneScrollOffset = newOffset;
                }
            }
        }

        // Horizontal scrollbar drag (runs even if mouse leaves panel); thumb follows cursor 1:1
        if (_horizontalScrollDragging && Input.MouseLeftDown && ViewState != null)
        {
            var hScrollBar = GetHorizontalScrollbarBounds(content);
            double totalRange = GetTotalTimeRange();
            double visibleDuration = hScrollBar.Width / (double)ViewState.Zoom;
            if (totalRange > 0 && visibleDuration < totalRange)
            {
                int thumbW = Math.Max(20, (int)(visibleDuration / totalRange * hScrollBar.Width));
                int travel = hScrollBar.Width - thumbW;
                if (travel > 0)
                {
                    int deltaPixel = Input.MousePosition.X - _horizontalScrollDragStartPixel;
                    double deltaTime = deltaPixel * totalRange / travel;
                    double maxStart = totalRange - visibleDuration;
                    ViewState.ViewStartTime = Math.Max(0, Math.Min(maxStart, _horizontalScrollDragStartTime + deltaTime));
                }
            }
        }

        bool inIgnoreRect = IgnoreClickRect.HasValue && IgnoreClickRect.Value.Contains(Input.MousePosition);
        if (ContainsPoint(Input.MousePosition) && !inIgnoreRect)
        {
            // Pan with middle mouse drag: grab the timeline so the point under the cursor stays under the cursor
            if (Input.MouseMiddlePressed && ViewState != null)
                _panDragStartViewTime = ViewState.ViewStartTime;
            if (Input.IsDragging && Input.MouseMiddleDown && ViewState != null)
            {
                ViewState.ViewStartTime = _panDragStartViewTime - Input.DragDelta.X / (double)ViewState.Zoom;
                ClampViewToTimeRange(content);
            }

            // Vertical scrollbar: start drag or click to jump
            var vBar = GetScrollbarBounds(content);
            if (Input.MouseLeftPressed && vBar.Contains(Input.MousePosition))
            {
                var trackArea = GetTrackContentBounds(content);
                int totalLanes = GetTotalLaneCount();
                int visibleLanes = GetVisibleLaneCount(trackArea);
                if (totalLanes > visibleLanes)
                {
                    var vThumb = GetVerticalScrollbarThumbBounds(content);
                    if (vThumb.Contains(Input.MousePosition))
                    {
                        _verticalScrollDragging = true;
                        _verticalScrollDragStartY = Input.MousePosition.Y;
                        _verticalScrollDragStartOffset = _laneScrollOffset;
                    }
                    else
                    {
                        // Click above/below thumb: jump so click position is at top of visible area
                        double range = totalLanes - visibleLanes;
                        int thumbHeight = Math.Max(MinScrollbarThumbHeight, (int)(visibleLanes / (double)totalLanes * vBar.Height));
                        int travel = vBar.Height - thumbHeight;
                        if (travel > 0)
                        {
                            double t = (Input.MousePosition.Y - vBar.Y - thumbHeight / 2) / (double)travel;
                            _laneScrollOffset = Math.Clamp((int)Math.Round(t * range), 0, (int)range);
                        }
                    }
                }
            }

            // Horizontal scrollbar: start drag or click to jump
            var hScrollBar = GetHorizontalScrollbarBounds(content);
            if (Input.MouseLeftPressed && hScrollBar.Contains(Input.MousePosition))
            {
                var thumb = GetHorizontalScrollbarThumbBounds(content);
                if (thumb.Contains(Input.MousePosition))
                {
                    _horizontalScrollDragging = true;
                    _horizontalScrollDragStartPixel = Input.MousePosition.X;
                    _horizontalScrollDragStartTime = ViewState!.ViewStartTime;
                }
                else
                {
                    // Click in track: jump view so click position is at 1/3 from left
                    double totalRange = GetTotalTimeRange();
                    double visibleDuration = hScrollBar.Width / (double)ViewState!.Zoom;
                    double timeAtClick = (Input.MousePosition.X - hScrollBar.X) / (double)hScrollBar.Width * totalRange;
                    ViewState.ViewStartTime = Math.Max(0, Math.Min(totalRange - visibleDuration, timeAtClick - visibleDuration / 3.0));
                }
            }

            // Mouse wheel: Alt = horizontal pan, Ctrl = zoom, else vertical scroll
            int wheel = Input.ScrollWheelDelta;
            if (wheel != 0)
            {
                var trackArea = GetTrackContentBounds(content);
                bool alt = Input.IsKeyDown(Keys.LeftAlt) || Input.IsKeyDown(Keys.RightAlt);
                bool ctrl = Input.IsKeyDown(Keys.LeftControl) || Input.IsKeyDown(Keys.RightControl);
                if (alt)
                {
                    ViewState.Pan(-wheel * 10f);
                    ClampViewToTimeRange(content);
                }
                else if (ctrl)
                {
                    var trackAreaZoom = GetTrackContentBounds(content);
                    ViewState.ZoomAt(wheel * 0.15f, Input.MousePosition.X, trackAreaZoom.X);
                    ClampViewToTimeRange(content);
                }
                else
                {
                    var scrollbar = GetScrollbarBounds(content);
                    if (trackArea.Contains(Input.MousePosition) || scrollbar.Contains(Input.MousePosition))
                    {
                        int totalLanes = GetTotalLaneCount();
                        int visibleLanes = GetVisibleLaneCount(trackArea);
                        ClampLaneScroll(visibleLanes, totalLanes);
                        _laneScrollOffset -= Math.Sign(wheel) * Math.Max(1, visibleLanes / 3);
                        ClampLaneScroll(visibleLanes, totalLanes);
                    }
                }
            }

            // Home/End: jump view to start or end of timeline (FL Studio style)
            if (Input.IsKeyPressed(Keys.Home))
            {
                ViewState.ViewStartTime = 0;
            }
            if (Input.IsKeyPressed(Keys.End))
            {
                double total = GetTotalTimeRange();
                var trackArea = GetTrackContentBounds(content);
                double visibleDuration = trackArea.Width / (double)ViewState.Zoom;
                ViewState.ViewStartTime = Math.Max(0, total - visibleDuration);
            }

            // Right click: delete event time if on a block, otherwise seek and start right-drag seeking
            if (Input.MouseRightPressed && Transport != null && ViewState != null)
            {
                var trackArea = GetTrackContentBounds(content);
                if (trackArea.Contains(Input.MousePosition) && Project?.EventTracks.Count > 0)
                {
                    var (hitTrack, hitTime, _, _) = HitTestEvent(content, Input.MousePosition.X, Input.MousePosition.Y);
                    if (hitTrack != null && hitTime.HasValue)
                    {
                        hitTrack.EventTimes.Remove(hitTime.Value);
                        if (hitTrack is EventTrackBase eb)
                            eb.EventDurations.Remove(hitTime.Value);
                        if (Selection?.SelectedEventTrack == hitTrack && Math.Abs((Selection.SelectedEventTime ?? -1) - hitTime.Value) < 0.0001)
                            Selection.SelectedEventTime = null;
                    }
                    else
                    {
                        double time = Math.Max(0, ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X));
                        if (SeekRequested != null)
                            SeekRequested(time);
                        else
                            Transport.Seek(time);
                        _rightDragSeeking = true;
                    }
                }
                else if (content.Contains(Input.MousePosition))
                {
                    var trackAreaForSeek = GetTrackContentBounds(content);
                    double time = Math.Max(0, ViewState.ScreenToTime(Input.MousePosition.X, trackAreaForSeek.X));
                    if (SeekRequested != null)
                        SeekRequested(time);
                    else
                        Transport.Seek(time);
                    _rightDragSeeking = true;
                }
            }

            // Left click: playhead bulb drag (head is above content), or click strip to teleport, or select/add event on timeline
            if (Input.MouseLeftPressed && Transport != null && ViewState != null)
            {
                var trackArea = GetTrackContentBounds(content);
                int bulbCenterY = content.Y - TimelinePlayheadRenderer.PlayheadHeadOffset;
                double t = double.IsFinite(PlayheadDisplayTime) ? PlayheadDisplayTime : Transport.CurrentTime;
                float playheadX = ViewState.TimeToScreen(t, trackArea.X);
                float dx = Input.MousePosition.X - playheadX;
                float dy = Input.MousePosition.Y - bulbCenterY;
                int r = TimelinePlayheadRenderer.PlayheadBulbRadius;
                if (dx * dx + dy * dy <= r * r)
                    _playheadBulbDragging = true;
                else if (GetPlayheadStripBounds(content).Contains(Input.MousePosition))
                {
                    double time = Math.Max(0, ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X));
                    bool smooth = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
                    if (!smooth)
                        time = Transport.SnapToBeat(time, Math.Max(1, ViewState.GridSubdivisionsPerBeat));
                    if (SeekRequested != null)
                        SeekRequested(time);
                    else
                        Transport.Seek(time);
                }
            }

            if (Input.MouseLeftPressed && Project?.EventTracks.Count > 0 && Transport != null && !_playheadBulbDragging)
            {
                var trackArea = GetTrackContentBounds(content);
                if (trackArea.Contains(Input.MousePosition))
                {
                    var (hitTrack, hitTime, hitLeftEdge, hitRightEdge) = HitTestEvent(content, Input.MousePosition.X, Input.MousePosition.Y);
                    if (hitTrack != null && hitTime.HasValue && (hitLeftEdge || hitRightEdge) && hitTrack is EventTrackBase resizeTrack)
                    {
                        _durationResizeTrack = resizeTrack;
                        _durationResizeEventTime = hitTime.Value;
                        _durationResizeFromLeft = hitLeftEdge;
                        if (Selection != null)
                        {
                            Selection.SelectedEventTrack = hitTrack;
                            Selection.SelectedEventTime = hitTime.Value;
                        }
                    }
                    else
                    {
                        double time = ViewState!.ScreenToTime(Input.MousePosition.X, trackArea.X);
                        double snappedTime = Transport.SnapToBeat(time, ViewState?.GridSubdivisionsPerBeat ?? 4);
                        if (hitTrack != null && hitTime.HasValue)
                        {
                            if (Selection != null)
                            {
                                Selection.SelectedEventTrack = hitTrack;
                                Selection.SelectedEventTime = hitTime.Value;
                            }
                            if (hitTrack is EventTrackBase moveTrack)
                            {
                                _noteMoveTrack = moveTrack;
                                _noteMoveEventTime = hitTime.Value;
                            }
                        }
                        else if (hitTrack != null)
                        {
                            if (!hitTrack.EventTimes.Contains(snappedTime))
                            {
                                hitTrack.EventTimes.Add(snappedTime);
                                if (hitTrack is EventTrackBase baseTrack)
                                    baseTrack.EventTimes.Sort();
                            }
                            if (Selection != null)
                            {
                                Selection.SelectedEventTrack = hitTrack;
                                Selection.SelectedEventTime = snappedTime;
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>FL Studio-style piano key strip on the left (same height as track, no h-scroll).</summary>
    private static Rectangle GetPianoStripBounds(Rectangle content)
    {
        int h = Math.Max(0, content.Height - HorizontalScrollbarHeight);
        return new Rectangle(content.X, content.Y, PianoStripWidth, h);
    }

    /// <summary>Note/grid area (right of piano strip), excluding horizontal and vertical scrollbars.</summary>
    private static Rectangle GetTrackContentBounds(Rectangle content)
    {
        int h = Math.Max(0, content.Height - HorizontalScrollbarHeight);
        int w = Math.Max(0, content.Width - ScrollbarWidth - PianoStripWidth);
        return new Rectangle(content.X + PianoStripWidth, content.Y, w, h);
    }

    /// <summary>Horizontal strip above the track area where the playhead head sits; clicking here teleports the playhead.</summary>
    private static Rectangle GetPlayheadStripBounds(Rectangle content)
    {
        var trackArea = GetTrackContentBounds(content);
        int stripTop = content.Y - TimelinePlayheadRenderer.PlayheadHeadOffset - TimelinePlayheadRenderer.PlayheadBulbRadius;
        int stripHeight = TimelinePlayheadRenderer.PlayheadHeadOffset + TimelinePlayheadRenderer.PlayheadBulbRadius;
        return new Rectangle(trackArea.X, stripTop, trackArea.Width, stripHeight);
    }

    private static Rectangle GetScrollbarBounds(Rectangle content)
    {
        int trackH = Math.Max(0, content.Height - HorizontalScrollbarHeight);
        return new Rectangle(content.Right - ScrollbarWidth, content.Y, ScrollbarWidth, trackH);
    }

    private Rectangle GetVerticalScrollbarThumbBounds(Rectangle content)
    {
        var scrollbar = GetScrollbarBounds(content);
        var trackArea = GetTrackContentBounds(content);
        int totalLanes = GetTotalLaneCount();
        int visibleLanes = GetVisibleLaneCount(trackArea);
        if (totalLanes <= visibleLanes || scrollbar.Height <= 0)
            return new Rectangle(scrollbar.X, scrollbar.Y, scrollbar.Width, scrollbar.Height);
        int thumbHeight = Math.Max(MinScrollbarThumbHeight, (int)(visibleLanes / (double)totalLanes * scrollbar.Height));
        double range = totalLanes - visibleLanes;
        int thumbY = scrollbar.Y + (range > 0 ? (int)(_laneScrollOffset / range * (scrollbar.Height - thumbHeight)) : 0);
        return new Rectangle(scrollbar.X + 2, thumbY, scrollbar.Width - 4, thumbHeight);
    }

    private static Rectangle GetHorizontalScrollbarBounds(Rectangle content)
    {
        int w = Math.Max(0, content.Width - ScrollbarWidth - PianoStripWidth);
        return new Rectangle(content.X + PianoStripWidth, content.Bottom - HorizontalScrollbarHeight, w, HorizontalScrollbarHeight);
    }

    private Rectangle GetHorizontalScrollbarThumbBounds(Rectangle content)
    {
        if (ViewState == null) return default;
        var bar = GetHorizontalScrollbarBounds(content);
        double total = GetTotalTimeRange();
        double visibleDuration = bar.Width / (double)ViewState.Zoom;
        if (total <= 0 || visibleDuration >= total)
            return new Rectangle(bar.X, bar.Y, bar.Width, bar.Height);
        int thumbW = Math.Max(20, (int)(visibleDuration / total * bar.Width));
        int thumbX = bar.X + (total > 0 ? (int)(ViewState.ViewStartTime / total * (bar.Width - thumbW)) : 0);
        return new Rectangle(thumbX, bar.Y + 2, thumbW, bar.Height - 4);
    }

    private double GetTotalTimeRange()
    {
        double fromWaveform = Waveform?.DurationSeconds ?? 0;
        double fromEvents = 0;
        if (Project?.EventTracks != null)
        {
            foreach (var t in Project.EventTracks)
            {
                foreach (var et in t.EventTimes)
                {
                    double endTime = et + GetEventDuration(t, et);
                    if (endTime > fromEvents) fromEvents = endTime;
                }
            }
            fromEvents += 10;
        }
        return Math.Max(MinTotalTimeRange, Math.Max(fromWaveform, fromEvents));
    }

    private void ClampViewToTimeRange(Rectangle content)
    {
        if (ViewState == null) return;
        var trackArea = GetTrackContentBounds(content);
        double total = GetTotalTimeRange();
        double visibleDuration = trackArea.Width / (double)ViewState.Zoom;
        ViewState.ViewStartTime = Math.Max(0, Math.Min(total - visibleDuration, ViewState.ViewStartTime));
    }

    private int GetVisibleLaneCount(Rectangle trackArea) => Math.Max(0, trackArea.Height / LaneHeight);

    private int GetTotalLaneCount()
    {
        if (Project == null || Project.EventTracks.Count == 0) return DefaultRowCount;
        return Math.Max(DefaultRowCount, Project.EventTracks.Count);
    }

    private void ClampLaneScroll(int visibleLanes, int totalLanes)
    {
        int maxOffset = Math.Max(0, totalLanes - visibleLanes);
        _laneScrollOffset = Math.Clamp(_laneScrollOffset, 0, maxOffset);
    }

    private static double GetEventDuration(IEventTrack track, double eventTime)
    {
        return (track as EventTrackBase)?.GetDuration(eventTime) ?? EventBlockDisplayDuration;
    }

    /// <summary>Returns (track, eventTime, hitLeftEdge, hitRightEdge) if click hit an event block; (track, null, false, false) if hit a track row but no block; (null, null, false, false) otherwise.</summary>
    private (IEventTrack? track, double? eventTime, bool hitLeftEdge, bool hitRightEdge) HitTestEvent(Rectangle content, int screenX, int screenY)
    {
        if (ViewState == null || Project?.EventTracks == null) return (null, null, false, false);
        var trackArea = GetTrackContentBounds(content);
        if (!trackArea.Contains(screenX, screenY)) return (null, null, false, false);
        int row = _laneScrollOffset + (screenY - trackArea.Y) / LaneHeight;
        if (row < 0 || row >= Project.EventTracks.Count) return (null, null, false, false);
        var track = Project.EventTracks[row];
        int blockY = trackArea.Y + (row - _laneScrollOffset) * LaneHeight + 2;
        int blockH = LaneHeight - 4;
        foreach (var et in track.EventTimes)
        {
            double dur = GetEventDuration(track, et);
            float x = ViewState.TimeToScreen(et, trackArea.X);
            float w = (float)(dur * ViewState.Zoom);
            int blockW = (int)Math.Max(2, w);
            var rect = new Rectangle((int)x, blockY, blockW, blockH);
            if (!rect.Contains(screenX, screenY)) continue;
            bool leftEdge = blockW >= NoteResizeHandleWidth && screenX < rect.X + NoteResizeHandleWidth;
            bool rightEdge = blockW >= NoteResizeHandleWidth && screenX >= rect.Right - NoteResizeHandleWidth;
            if (leftEdge && rightEdge)
                return (track, et, screenX < rect.X + blockW / 2, !(screenX < rect.X + blockW / 2));
            return (track, et, leftEdge, rightEdge);
        }
        return (track, null, false, false);
    }

    protected override void DrawContent(SpriteBatch spriteBatch)
    {
        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);
        var content = ContentBounds;

        if (ViewState == null)
        {
            spriteBatch.Draw(pixel, new Rectangle(content.X + content.Width / 4, content.Y, 1, content.Height), new Color(60, 63, 70));
            return;
        }

        var trackArea = GetTrackContentBounds(content);
        var pianoStrip = GetPianoStripBounds(content);

        // FL Studio-style piano strip (alternating light/dark rows like piano keys)
        for (int row = 0; row * LaneHeight < pianoStrip.Height; row++)
        {
            var rowRect = new Rectangle(pianoStrip.X, pianoStrip.Y + row * LaneHeight, pianoStrip.Width, LaneHeight);
            if (rowRect.Bottom > pianoStrip.Bottom) rowRect.Height = pianoStrip.Bottom - rowRect.Y;
            var stripColor = (row & 1) == 0 ? PianoStripLight : PianoStripDark;
            spriteBatch.Draw(pixel, rowRect, stripColor);
        }
        // Separator between piano strip and note area
        spriteBatch.Draw(pixel, new Rectangle(trackArea.X - 1, content.Y, 1, pianoStrip.Height), new Color(22, 24, 28));

        // Note area background (slightly darker than panel for FL look)
        spriteBatch.Draw(pixel, trackArea, new Color(24, 26, 30));

        // Alternating lane rows (draw before waveform so waveform stays visible on top)
        for (int row = 0; row * LaneHeight < trackArea.Height; row++)
        {
            if ((row & 1) == 1) continue;
            var rowRect = new Rectangle(trackArea.X, trackArea.Y + row * LaneHeight, trackArea.Width, LaneHeight);
            if (rowRect.Bottom > trackArea.Bottom) rowRect.Height = trackArea.Bottom - rowRect.Y;
            spriteBatch.Draw(pixel, rowRect, new Color(30, 32, 36));
        }

        // Waveform as background of the piano roll (behind grid and notes, on top of lane stripes)
        if (Waveform != null && Waveform.BucketCount > 0)
            WaveformRenderer.Draw(spriteBatch, pixel, trackArea, Waveform, ViewState, trackArea.X);

        // Grid (note area only)
        double bpm = Transport?.BPM ?? Project?.BPM ?? 120;
        int num = Project?.TimeSignatureNumerator ?? 4;
        int den = Project?.TimeSignatureDenominator ?? 4;
        double beatOffset = Project?.BeatOffsetSeconds ?? 0;
        TimelineGridRenderer.Draw(spriteBatch, pixel, trackArea, ViewState, bpm, num, den, beatOffset);

        // Event blocks — FL Studio style: rounded corners and vertical gradient
        int visibleLanes = GetVisibleLaneCount(trackArea);
        int totalLanes = GetTotalLaneCount();
        double viewEnd = ViewState.ViewEndTime(trackArea.Width);
        EnsureNoteTextures(spriteBatch.GraphicsDevice);
        Texture2D fillTex = pixel;
        Texture2D borderTex = pixel;
        if (Project?.EventTracks != null && s_noteFillTexture != null && s_noteBorderTexture != null && s_noteSelectedFillTexture != null && s_noteSelectedBorderTexture != null)
        {
            for (int row = 0; row < Project.EventTracks.Count; row++)
            {
                int visibleRowIndex = row - _laneScrollOffset;
                if (visibleRowIndex < 0 || visibleRowIndex >= visibleLanes)
                    continue;
                var track = Project.EventTracks[row];
                foreach (var eventTime in track.EventTimes)
                {
                    double dur = GetEventDuration(track, eventTime);
                    if (eventTime + dur < ViewState.ViewStartTime || eventTime > viewEnd)
                        continue;
                    float fx = ViewState.TimeToScreen(eventTime, trackArea.X);
                    float w = (float)(dur * ViewState.Zoom);
                    int y = trackArea.Y + visibleRowIndex * LaneHeight + 2;
                    int h = LaneHeight - 4;
                    int blockW = (int)Math.Max(2, w);
                    bool selected = Selection != null && Selection.SelectedEventTrack == track && Selection.SelectedEventTime.HasValue && Math.Abs(Selection.SelectedEventTime.Value - eventTime) < 0.0001;
                    fillTex = selected ? s_noteSelectedFillTexture : s_noteFillTexture;
                    borderTex = selected ? s_noteSelectedBorderTexture : s_noteBorderTexture;
                    int x = (int)fx;
                    // Border behind (1px outline)
                    spriteBatch.Draw(borderTex, new Rectangle(x - 1, y - 1, blockW + 2, h + 2), new Rectangle(0, 0, NoteTextureWidth, NoteTextureHeight), Color.White);
                    spriteBatch.Draw(fillTex, new Rectangle(x, y, blockW, h), new Rectangle(0, 0, NoteTextureWidth, NoteTextureHeight), Color.White);
                    // Resize handles: darker strips on left and right edges, inset vertically so rounded corners show
                    if (blockW >= NoteResizeHandleWidth && h > NoteCornerRadius * 2)
                    {
                        int handleW = Math.Min(3, Math.Max(2, blockW / 6));
                        int inset = NoteCornerRadius;
                        spriteBatch.Draw(pixel, new Rectangle(x, y + inset, handleW, h - inset * 2), NoteResizeHandle);
                        spriteBatch.Draw(pixel, new Rectangle(x + blockW - handleW, y + inset, handleW, h - inset * 2), NoteResizeHandle);
                    }
                }
            }
        }

        // Playhead on top of notes (use smoothed display time from game to reduce audio position jitter)
        if (Transport != null)
        {
            double t = double.IsFinite(PlayheadDisplayTime) ? PlayheadDisplayTime : Transport.CurrentTime;
            float playheadX = ViewState.TimeToScreen(t, trackArea.X);
            TimelinePlayheadRenderer.Draw(spriteBatch, pixel, content, playheadX);
        }

        // Vertical scrollbar for lanes
        if (totalLanes > visibleLanes)
        {
            var scrollbar = GetScrollbarBounds(content);
            spriteBatch.Draw(pixel, scrollbar, new Color(45, 48, 55));
            double range = totalLanes - visibleLanes;
            int thumbHeight = Math.Max(MinScrollbarThumbHeight, (int)(visibleLanes / (double)totalLanes * scrollbar.Height));
            int thumbY = scrollbar.Y + (range > 0 ? (int)(_laneScrollOffset / range * (scrollbar.Height - thumbHeight)) : 0);
            var thumb = new Rectangle(scrollbar.X + 2, thumbY, scrollbar.Width - 4, thumbHeight);
            spriteBatch.Draw(pixel, thumb, new Color(90, 95, 105));
        }

        // Horizontal scrollbar (time / FL Studio style)
        var hBar = GetHorizontalScrollbarBounds(content);
        spriteBatch.Draw(pixel, hBar, new Color(45, 48, 55));
        var hThumb = GetHorizontalScrollbarThumbBounds(content);
        if (hThumb.Width > 0)
            spriteBatch.Draw(pixel, hThumb, new Color(90, 95, 105));
    }
}
