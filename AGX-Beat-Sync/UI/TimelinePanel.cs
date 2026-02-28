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
    private const int HorizontalScrollbarHeight = 20;
    private const int ScrollbarWidth = 12;
    private const int MinScrollbarThumbHeight = 24;
    private const int MinHorizontalScrollbarThumbWidth = 56;
    private const double MinTotalTimeRange = 60.0;
    /// <summary>Width of the draggable strip at each end of the scrollbar thumb (FL Studio-style resize).</summary>
    private const int ScrollbarEdgeResizeWidth = 8;

    /// <summary>First visible row index for vertical scroll (each row = one event track).</summary>
    private int _laneScrollOffset;
    /// <summary>Horizontal scrollbar: dragging the time thumb.</summary>
    private bool _horizontalScrollDragging;
    private int _horizontalScrollDragStartPixel;
    private double _horizontalScrollDragStartTime;
    /// <summary>Horizontal scrollbar: dragging left edge of thumb to resize view (zoom from left).</summary>
    private bool _horizontalScrollResizeLeftEdge;
    /// <summary>Horizontal scrollbar: dragging right edge of thumb to resize view (zoom from right).</summary>
    private bool _horizontalScrollResizeRightEdge;
    /// <summary>Vertical scrollbar: dragging the lane thumb.</summary>
    private bool _verticalScrollDragging;
    private int _verticalScrollDragStartY;
    private int _verticalScrollDragStartOffset;
    /// <summary>Dragging the playhead bulb to seek.</summary>
    private bool _playheadBulbDragging;
    /// <summary>Click/drag on the playhead strip: playhead follows mouse while left button is held.</summary>
    private bool _playheadStripDragging;
    /// <summary>Dragging the in/out rectangle left edge (In point) in the in/out strip.</summary>
    private bool _inOutResizeLeft;
    /// <summary>Dragging the in/out rectangle right edge (Out point) in the in/out strip.</summary>
    private bool _inOutResizeRight;
    /// <summary>Dragging the in/out rectangle body to move the entire range (preserve duration).</summary>
    private bool _inOutRectDragging;
    private double _inOutRectDragStartInTime;
    private double _inOutRectDragStartOutTime;
    private int _inOutRectDragStartMouseX;
    /// <summary>Right-drag seeking: playhead follows mouse while right button is held.</summary>
    private bool _rightDragSeeking;
    /// <summary>Middle-drag pan: ViewStartTime when drag started so we apply total DragDelta once (grab-to-pan).</summary>
    private double _panDragStartViewTime;
    /// <summary>Dragging note edge to shorten/lengthen (Ableton/FL style).</summary>
    private EventTrackBase? _durationResizeTrack;
    private double _durationResizeEventTime;
    /// <summary>True = dragging left edge (move start); false = dragging right edge (move end).</summary>
    private bool _durationResizeFromLeft;
    /// <summary>Set on edge click; promoted to _durationResizeTrack only when user actually drags (so a simple click just selects).</summary>
    private EventTrackBase? _pendingResizeTrack;
    private double _pendingResizeEventTime;
    private bool _pendingResizeFromLeft;
    /// <summary>Dragging note body to move note to new time/track.</summary>
    private EventTrackBase? _noteMoveTrack;
    private double _noteMoveEventTime;
    /// <summary>Time offset from note start to the point where the user grabbed (cursor time at press minus note start). Keeps the note under the cursor where it was grabbed.</summary>
    private double _noteMoveGrabOffsetSeconds;
    /// <summary>Set on note body click; promoted to _noteMoveTrack only when user actually drags (so a simple click just selects).</summary>
    private EventTrackBase? _pendingNoteMoveTrack;
    private double _pendingNoteMoveEventTime;
    /// <summary>When moving multiple notes: (track, eventTime, duration, rowOffset from anchor, timeOffset from anchor) for each non-anchor note.</summary>
    private List<(EventTrackBase track, double eventTime, double duration, int rowOffset, double timeOffset)>? _noteMoveMultiOthers;
    /// <summary>Current (track, time) of each note in _noteMoveMultiOthers as we drag.</summary>
    private List<(EventTrackBase track, double time)>? _noteMoveMultiCurrent;
    private const int NoteMoveDragThresholdPx = 3;
    /// <summary>Minimum drag distance (pixels) before a press on empty space is treated as rectangle selection instead of add-note.</summary>
    private const int RectSelectDragThresholdPx = 4;
    private const int NoteResizeHandleWidth = 8;
    /// <summary>When a note is narrower than this (zoomed out), only move/select is possible; resize handles are disabled.</summary>
    private const int MinNoteWidthForResize = 24;
    private const double MinNoteDurationSeconds = 0.01;
    private const int InOutEdgeHandleWidth = 6;
    /// <summary>Height of the dedicated in/out strip (above the playhead strip).</summary>
    private const int InOutStripHeight = 14;
    /// <summary>Duration (seconds) for the next note drawn. Updated when user clicks a note so the next drawn note matches that size.</summary>
    private double _nextNoteDurationSeconds = EventTrackConstants.DefaultEventDurationSeconds;
    /// <summary>Left press was on empty track space; we may start rectangle selection if user drags.</summary>
    private bool _pendingRectSelect;
    private Point _rectSelectStart;
    /// <summary>User is dragging a selection rectangle (click and hold on empty space).</summary>
    private bool _rectangleSelecting;

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
    /// <summary>Note fill and border use white so track tint shows as the actual color (no orange baked in).</summary>
    private static readonly Color NoteFillTop = new(255, 255, 255);
    private static readonly Color NoteFillBottom = new(248, 248, 248);
    private static readonly Color NoteBorder = new(255, 255, 255);
    private static readonly Color NoteSelectedFillTop = new(255, 255, 255);
    private static readonly Color NoteSelectedFillBottom = new(252, 252, 252);
    private static readonly Color NoteSelectedBorder = new(255, 255, 255);
    /// <summary>Outer ring drawn around selected notes so selection is obvious.</summary>
    private static readonly Color NoteSelectionOutline = new(255, 255, 255);
    private static readonly Color NoteResizeHandle = new(255, 255, 255);

    /// <summary>Note texture: rounded rect with large corner radius so it reads clearly when scaled (pill shape).</summary>
    private const int NoteTextureWidth = 128;
    private const int NoteTextureHeight = 48;
    /// <summary>Corner radius in texture — large so scaled notes stay obviously rounded, not square.</summary>
    private const int NoteTextureCornerRadius = 16;
    /// <summary>Corner radius when drawing — FL Studio style: nicely rounded corners (used for 9-slice and resize handle inset).</summary>
    private const int NoteCornerRadius = 8;
    private static Texture2D? s_noteFillTexture;
    private static Texture2D? s_noteSelectedFillTexture;
    private static Texture2D? s_noteBorderTexture;
    private static Texture2D? s_noteSelectedBorderTexture;
    /// <summary>Pill-shaped texture for resize handles (anti-aliased rounded ends).</summary>
    private static Texture2D? s_resizeHandlePillTexture;
    private static GraphicsDevice? s_noteTextureDevice;
    private const int ResizeHandlePillWidth = 8;
    private const int ResizeHandlePillHeight = 32;
    private const int ResizeHandlePillRadius = 4;

    /// <summary>Reserved height for in/out strip + playhead strip so both sit inside the panel.</summary>
    private static int StripsHeight => InOutStripHeight + TimelinePlayheadRenderer.PlayheadHeadOffset + TimelinePlayheadRenderer.PlayheadBulbRadius;

    public override Rectangle ContentBounds =>
        new(Bounds.X, Bounds.Y + HeaderHeight + StripsHeight, Bounds.Width, Math.Max(0, Bounds.Height - HeaderHeight - StripsHeight));

    public TimelinePanel()
    {
        Title = "Timeline";
        BackgroundColor = PianoRollBackground;
    }

    /// <summary>Returns coverage 0..1 for anti-aliased rounded-rect; pixel center at (px+0.5, py+0.5).</summary>
    private static float GetRoundedRectCoverage(float px, float py, int w, int h, int r)
    {
        if (r <= 0) return (px >= 0 && px < w && py >= 0 && py < h) ? 1f : 0f;
        float cx = px + 0.5f;
        float cy = py + 0.5f;
        // Center rect: full coverage
        if (cx >= r && cx < w - r && cy >= r && cy < h - r) return 1f;
        // Top-left corner
        if (cx < r && cy < r)
        {
            float dx = cx - r, dy = cy - r;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            return Math.Clamp((r + 0.5f - d) / 1f, 0f, 1f);
        }
        // Top-right corner
        if (cx >= w - r && cy < r)
        {
            float dx = cx - (w - r), dy = cy - r;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            return Math.Clamp((r + 0.5f - d) / 1f, 0f, 1f);
        }
        // Bottom-left corner
        if (cx < r && cy >= h - r)
        {
            float dx = cx - r, dy = cy - (h - r);
            float d = MathF.Sqrt(dx * dx + dy * dy);
            return Math.Clamp((r + 0.5f - d) / 1f, 0f, 1f);
        }
        // Bottom-right corner
        if (cx >= w - r && cy >= h - r)
        {
            float dx = cx - (w - r), dy = cy - (h - r);
            float d = MathF.Sqrt(dx * dx + dy * dy);
            return Math.Clamp((r + 0.5f - d) / 1f, 0f, 1f);
        }
        // Straight edges
        return 1f;
    }

    private static Color Darken(Color c, float factor)
    {
        return new Color(
            (byte)Math.Clamp((int)(c.R * factor), 0, 255),
            (byte)Math.Clamp((int)(c.G * factor), 0, 255),
            (byte)Math.Clamp((int)(c.B * factor), 0, 255));
    }

    private static Color Lighten(Color c, float factor)
    {
        return new Color(
            (byte)Math.Clamp((int)(c.R * factor), 0, 255),
            (byte)Math.Clamp((int)(c.G * factor), 0, 255),
            (byte)Math.Clamp((int)(c.B * factor), 0, 255));
    }

    private static Texture2D CreateRoundedRectGradient(GraphicsDevice gd, int w, int h, int r, Color top, Color bottom)
    {
        var data = new Color[w * h];
        for (int py = 0; py < h; py++)
        {
            float t = h <= 1 ? 1f : (float)py / (h - 1);
            byte cr = (byte)(top.R + (bottom.R - top.R) * t);
            byte cg = (byte)(top.G + (bottom.G - top.G) * t);
            byte cb = (byte)(top.B + (bottom.B - top.B) * t);
            for (int px = 0; px < w; px++)
            {
                float coverage = GetRoundedRectCoverage(px, py, w, h, r);
                byte a = (byte)(Math.Clamp(coverage, 0f, 1f) * 255f);
                data[py * w + px] = new Color(cr, cg, cb, a);
            }
        }
        var tex = new Texture2D(gd, w, h);
        tex.SetData(data);
        return tex;
    }

    private static Texture2D CreateRoundedRectSolid(GraphicsDevice gd, int w, int h, int r, Color fill)
    {
        var data = new Color[w * h];
        for (int py = 0; py < h; py++)
        {
            for (int px = 0; px < w; px++)
            {
                float coverage = GetRoundedRectCoverage(px, py, w, h, r);
                byte a = (byte)(Math.Clamp(coverage, 0f, 1f) * 255f);
                data[py * w + px] = new Color(fill.R, fill.G, fill.B, a);
            }
        }
        var tex = new Texture2D(gd, w, h);
        tex.SetData(data);
        return tex;
    }

    /// <summary>Draws a rounded-rect texture using 9-slice so corners stay circular when the note is stretched. Uses high-res texture corners scaled down for crisp edges.</summary>
    private static void DrawRoundedRect9Slice(SpriteBatch sb, Texture2D tex, int x, int y, int blockW, int h, Color tint)
    {
        int r = NoteCornerRadius;
        int r2 = r * 2;
        int tr = NoteTextureCornerRadius;
        int tr2 = tr * 2;
        // Keep corners at fixed pixel size; only stretch center/edges. For very small notes, shrink effective radius.
        int er = Math.Min(r, Math.Min(Math.Max(0, (blockW - 1) / 2), Math.Max(0, (h - 1) / 2)));
        if (er <= 0)
        {
            sb.Draw(tex, new Rectangle(x, y, Math.Max(1, blockW), Math.Max(1, h)), new Rectangle(0, 0, NoteTextureWidth, NoteTextureHeight), tint);
            return;
        }
        int leftW = er;
        int rightW = er;
        int centerW = blockW - leftW - rightW;
        int topH = er;
        int bottomH = er;
        int centerH = h - topH - bottomH;
        int sw = NoteTextureWidth;
        int sh = NoteTextureHeight;
        // Source uses texture corner size (tr) so we scale down for crisp corners; destination uses display size (er).
        int srcL = 0, srcR = tr, srcCx = tr, srcCw = sw - tr2, srcRx = sw - tr;
        int srcT = 0, srcM = tr, srcCy = tr, srcCh = sh - tr2, srcBy = sh - tr;
        // Top row
        sb.Draw(tex, new Rectangle(x, y, leftW, topH), new Rectangle(srcL, srcT, tr, tr), tint);
        if (centerW > 0)
            sb.Draw(tex, new Rectangle(x + leftW, y, centerW, topH), new Rectangle(srcR, srcT, srcCw, tr), tint);
        sb.Draw(tex, new Rectangle(x + blockW - rightW, y, rightW, topH), new Rectangle(srcRx, srcT, tr, tr), tint);
        // Middle row
        if (centerH > 0)
        {
            sb.Draw(tex, new Rectangle(x, y + topH, leftW, centerH), new Rectangle(srcL, srcM, tr, srcCh), tint);
            if (centerW > 0)
                sb.Draw(tex, new Rectangle(x + leftW, y + topH, centerW, centerH), new Rectangle(srcR, srcM, srcCw, srcCh), tint);
            sb.Draw(tex, new Rectangle(x + blockW - rightW, y + topH, rightW, centerH), new Rectangle(srcRx, srcM, tr, srcCh), tint);
        }
        // Bottom row
        sb.Draw(tex, new Rectangle(x, y + h - bottomH, leftW, bottomH), new Rectangle(srcL, srcBy, tr, tr), tint);
        if (centerW > 0)
            sb.Draw(tex, new Rectangle(x + leftW, y + h - bottomH, centerW, bottomH), new Rectangle(srcR, srcBy, srcCw, tr), tint);
        sb.Draw(tex, new Rectangle(x + blockW - rightW, y + h - bottomH, rightW, bottomH), new Rectangle(srcRx, srcBy, tr, tr), tint);
    }

    private void EnsureNoteTextures(GraphicsDevice gd)
    {
        if (s_noteTextureDevice != gd)
        {
            s_noteFillTexture?.Dispose();
            s_noteSelectedFillTexture?.Dispose();
            s_noteBorderTexture?.Dispose();
            s_noteSelectedBorderTexture?.Dispose();
            s_resizeHandlePillTexture?.Dispose();
            s_noteTextureDevice = gd;
            s_noteFillTexture = null;
            s_noteSelectedFillTexture = null;
            s_noteBorderTexture = null;
            s_noteSelectedBorderTexture = null;
            s_resizeHandlePillTexture = null;
        }
        if (s_noteFillTexture == null)
        {
            s_noteFillTexture = CreateRoundedRectGradient(gd, NoteTextureWidth, NoteTextureHeight, NoteTextureCornerRadius, NoteFillTop, NoteFillBottom);
            s_noteSelectedFillTexture = CreateRoundedRectGradient(gd, NoteTextureWidth, NoteTextureHeight, NoteTextureCornerRadius, NoteSelectedFillTop, NoteSelectedFillBottom);
            s_noteBorderTexture = CreateRoundedRectSolid(gd, NoteTextureWidth, NoteTextureHeight, NoteTextureCornerRadius, NoteBorder);
            s_noteSelectedBorderTexture = CreateRoundedRectSolid(gd, NoteTextureWidth, NoteTextureHeight, NoteTextureCornerRadius, NoteSelectedBorder);
            s_resizeHandlePillTexture = CreateRoundedRectSolid(gd, ResizeHandlePillWidth, ResizeHandlePillHeight, ResizeHandlePillRadius, NoteResizeHandle);
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
            // In/Out strip (own line above playhead): only in/out tooltips
            if (GetInOutStripBounds(content).Contains(mouse))
            {
                double? inT = Project?.InTime;
                double? outT = Project?.OutTime;
                if (inT.HasValue && outT.HasValue)
                {
                    float inX = ViewState.TimeToScreen(inT.Value, trackArea.X);
                    float outX = ViewState.TimeToScreen(outT.Value, trackArea.X);
                    int leftEdgeRight = (int)inX + InOutEdgeHandleWidth;
                    int rightEdgeLeft = (int)outX - InOutEdgeHandleWidth;
                    if (mouse.X >= (int)inX && mouse.X < leftEdgeRight)
                        return "Drag to set In (I = set at playhead) — Shift: no snap";
                    if (mouse.X >= rightEdgeLeft && mouse.X < (int)outX)
                        return "Drag to set Out (O = set at playhead) — Shift: no snap";
                    if (mouse.X >= leftEdgeRight && mouse.X < rightEdgeLeft)
                        return "Drag to move In/Out range — Shift: no snap";
                }
                return "In/Out range (I = set In, O = set Out)";
            }
            if (GetPlayheadStripBounds(content).Contains(mouse))
                return "Click or drag to move playhead (Shift: no snap)";
        }

        if (!ContentBounds.Contains(mouse)) return null;

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
            // Hovering over timeline track area: show timecode at cursor
            double hoverTime = Math.Max(0, ViewState.ScreenToTime(mouse.X, trackArea.X));
            return $"Timeline : {TimeFormatHelper.Format(hoverTime)}";
        }

        return "Timeline";
    }

    public override MouseCursor? GetDesiredCursor(Point mouse)
    {
        if (ViewState == null) return null;
        if (_durationResizeTrack != null) return MouseCursor.SizeWE;
        if (_inOutRectDragging) return MouseCursor.SizeAll;
        var content = ContentBounds;
        var trackArea = GetTrackContentBounds(content);
        // Strips are above content; check them before ContentBounds
        if (GetInOutStripBounds(content).Contains(mouse))
        {
            double? inT = Project?.InTime;
            double? outT = Project?.OutTime;
            if (inT.HasValue && outT.HasValue && outT.Value > inT.Value)
            {
                float inX = ViewState.TimeToScreen(inT.Value, trackArea.X);
                float outX = ViewState.TimeToScreen(outT.Value, trackArea.X);
                int leftEdgeRight = (int)inX + InOutEdgeHandleWidth;
                int rightEdgeLeft = (int)outX - InOutEdgeHandleWidth;
                if (mouse.X >= (int)inX && mouse.X < leftEdgeRight) return MouseCursor.SizeWE;
                if (mouse.X >= rightEdgeLeft && mouse.X < (int)outX) return MouseCursor.SizeWE;
                if (mouse.X >= leftEdgeRight && mouse.X < rightEdgeLeft) return MouseCursor.SizeAll;
            }
        }
        if (!ContentBounds.Contains(mouse)) return null;
        if (!trackArea.Contains(mouse)) return null;
        var (_, _, hitLeftEdge, hitRightEdge) = HitTestEvent(content, mouse.X, mouse.Y);
        return (hitLeftEdge || hitRightEdge) ? MouseCursor.SizeWE : null;
    }

    /// <summary>Keep playhead in view: pan view when current time scrolls off left or right. Pass seekTime when we just sought (use that instead of Transport).</summary>
    private void FollowPlayheadIfOutOfView(double? seekTime = null)
    {
        if (ViewState == null) return;
        double t = seekTime ?? (Transport != null ? Transport.CurrentTime : 0);
        var content = ContentBounds;
        var trackArea = GetTrackContentBounds(content);
        double viewEnd = ViewState.ViewEndTime(trackArea.Width);

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

        // End horizontal/vertical scroll, playhead bulb/strip drag, note resize, note move, and rectangle selection on left release (anywhere)
        if (Input.MouseLeftReleased)
        {
            // Apply cut only on release: notes under each moved note are cut now
            if (_noteMoveTrack != null)
            {
                double duration = _noteMoveTrack.GetDuration(_noteMoveEventTime);
                CutTrackWithRange(_noteMoveTrack, _noteMoveEventTime, _noteMoveEventTime + duration, _noteMoveEventTime);
                if (_noteMoveMultiCurrent != null && _noteMoveMultiOthers != null)
                {
                    for (int i = 0; i < _noteMoveMultiCurrent.Count && i < _noteMoveMultiOthers.Count; i++)
                    {
                        var (track, time) = _noteMoveMultiCurrent[i];
                        double dur = _noteMoveMultiOthers[i].duration;
                        CutTrackWithRange(track, time, time + dur, time);
                    }
                }
            }
            // Complete rectangle selection: select all notes that intersect the dragged rect
            if (_rectangleSelecting && ViewState != null && Selection != null && Project?.EventTracks != null)
            {
                var trackArea = GetTrackContentBounds(content);
                var rect = GetNormalizedRect(_rectSelectStart, Input.MousePosition);
                var notesInRect = GetNotesInScreenRect(content, rect.X, rect.Y, rect.Right, rect.Bottom);
                Selection.SetSelectedNotes(notesInRect);
            }
            // If we had pending rect select but never dragged, add a note at the click position
            else if (_pendingRectSelect && ViewState != null && Transport != null && Project?.EventTracks != null)
            {
                var trackArea = GetTrackContentBounds(content);
                if (trackArea.Contains(_rectSelectStart))
                {
                    int row = _laneScrollOffset + (_rectSelectStart.Y - trackArea.Y) / LaneHeight;
                    if (row >= 0 && row < Project.EventTracks.Count)
                    {
                        var hitTrack = Project.EventTracks[row];
                        double time = ViewState.ScreenToTime(_rectSelectStart.X, trackArea.X);
                        double snappedTime = Transport.SnapToBeat(time, ViewState.GridSubdivisionsPerBeat);
                        double duration = Math.Max(MinNoteDurationSeconds, _nextNoteDurationSeconds);
                        if (!EventsOverlap(hitTrack, snappedTime, duration, null))
                        {
                            hitTrack.EventTimes.Add(snappedTime);
                            if (hitTrack is EventTrackBase baseTrack)
                            {
                                baseTrack.EventTimes.Sort();
                                baseTrack.SetDuration(snappedTime, duration);
                            }
                        }
                        if (Selection != null)
                            Selection.SetSingleNote(hitTrack, snappedTime);
                    }
                }
            }
            _rectangleSelecting = false;
            _pendingRectSelect = false;
            _horizontalScrollDragging = false;
            _horizontalScrollResizeLeftEdge = false;
            _horizontalScrollResizeRightEdge = false;
            _verticalScrollDragging = false;
            _playheadBulbDragging = false;
            _playheadStripDragging = false;
            _inOutResizeLeft = false;
            _inOutResizeRight = false;
            _inOutRectDragging = false;
            _durationResizeTrack = null;
            _pendingResizeTrack = null;
            _noteMoveTrack = null;
            _pendingNoteMoveTrack = null;
            _noteMoveMultiOthers = null;
            _noteMoveMultiCurrent = null;
        }

        // Promote pending note move to actual move only when user drags past threshold (not on simple click)
        if (_pendingNoteMoveTrack != null && Input.MouseLeftDown && Input.DragStart.HasValue && ViewState != null && Project?.EventTracks != null)
        {
            var delta = Input.DragDelta;
            if (Math.Abs(delta.X) >= NoteMoveDragThresholdPx || Math.Abs(delta.Y) >= NoteMoveDragThresholdPx)
            {
                var trackArea = GetTrackContentBounds(content);
                double grabTime = ViewState.ScreenToTime(Input.DragStart.Value.X, trackArea.X);
                _noteMoveTrack = _pendingNoteMoveTrack;
                _noteMoveEventTime = _pendingNoteMoveEventTime;
                _noteMoveGrabOffsetSeconds = grabTime - _noteMoveEventTime;
                _pendingNoteMoveTrack = null;

                // If multiple notes selected and we're dragging one of them, move all (only EventTrackBase supports move)
                if (Selection != null && Selection.SelectedNotes.Count > 1 && Selection.IsNoteSelected(_noteMoveTrack, _noteMoveEventTime))
                {
                    int anchorRow = Project.EventTracks.IndexOf(_noteMoveTrack);
                    if (anchorRow < 0) anchorRow = 0;
                    _noteMoveMultiOthers = new List<(EventTrackBase track, double eventTime, double duration, int rowOffset, double timeOffset)>();
                    _noteMoveMultiCurrent = new List<(EventTrackBase track, double time)>();
                    foreach (var (track, eventTime) in Selection.SelectedNotes)
                    {
                        if (track == _noteMoveTrack && Math.Abs(eventTime - _noteMoveEventTime) < 0.0001)
                            continue;
                        if (track is not EventTrackBase baseTrack) continue;
                        int row = Project.EventTracks.IndexOf(baseTrack);
                        if (row < 0) continue;
                        double dur = GetEventDuration(track, eventTime);
                        int rowOffset = row - anchorRow;
                        double timeOffset = eventTime - _noteMoveEventTime;
                        _noteMoveMultiOthers.Add((baseTrack, eventTime, dur, rowOffset, timeOffset));
                        _noteMoveMultiCurrent.Add((baseTrack, eventTime));
                    }
                    if (_noteMoveMultiOthers.Count == 0)
                    {
                        _noteMoveMultiOthers = null;
                        _noteMoveMultiCurrent = null;
                    }
                }
            }
        }

        // Promote pending resize to actual resize only when user drags (not on simple click)
        if (_pendingResizeTrack != null && Input.MouseLeftDown && Input.IsDragging)
        {
            _durationResizeTrack = _pendingResizeTrack;
            _durationResizeEventTime = _pendingResizeEventTime;
            _durationResizeFromLeft = _pendingResizeFromLeft;
            _pendingResizeTrack = null;
        }

        // Promote pending rect select to actual rectangle selection when user drags past threshold (click and hold = rect select)
        if (_pendingRectSelect && Input.MouseLeftDown && Input.DragStart.HasValue)
        {
            var delta = Input.DragDelta;
            if (Math.Abs(delta.X) >= RectSelectDragThresholdPx || Math.Abs(delta.Y) >= RectSelectDragThresholdPx)
            {
                _rectangleSelecting = true;
                _pendingRectSelect = true; // keep so we don't add note on release
            }
        }
        if (Input.MouseRightReleased)
            _rightDragSeeking = false;

        // Update playhead position while dragging the bulb or the strip (snap by default, Shift = smooth)
        if ((_playheadBulbDragging || _playheadStripDragging) && Input.MouseLeftDown && ViewState != null && Transport != null)
        {
            var trackArea = GetTrackContentBounds(content);
            double time = Math.Max(0, ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X));
            bool smooth = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
            if (!smooth)
                time = Transport.SnapToBeat(time, Math.Max(1, ViewState.GridSubdivisionsPerBeat));
            time = ClampSeekTime(time);
            if (SeekRequested != null)
                SeekRequested(time);
            else
                Transport.Seek(time);
            FollowPlayheadIfOutOfView(time);
        }

        // In/Out rectangle edge drag: update InTime or OutTime from mouse X (snap to grid by default, Shift = smooth)
        if (Project != null && ViewState != null && Transport != null && (_inOutResizeLeft || _inOutResizeRight) && Input.MouseLeftDown)
        {
            var trackArea = GetTrackContentBounds(content);
            double time = Math.Max(0, ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X));
            bool smooth = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
            if (!smooth)
                time = Transport.SnapToBeat(time, Math.Max(1, ViewState.GridSubdivisionsPerBeat));
            if (_inOutResizeLeft)
            {
                Project.InTime = time;
                if (Project.OutTime.HasValue && Project.OutTime.Value < time)
                    Project.OutTime = time;
            }
            else
            {
                Project.OutTime = time;
                if (Project.InTime.HasValue && Project.InTime.Value > time)
                    Project.InTime = time;
            }
        }

        // In/Out rectangle body drag: move entire range (snap to grid by default, Shift = smooth)
        if (Project != null && ViewState != null && Transport != null && _inOutRectDragging && Input.MouseLeftDown)
        {
            var trackArea = GetTrackContentBounds(content);
            double startTime = ViewState.ScreenToTime(_inOutRectDragStartMouseX, trackArea.X);
            double currentTime = ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X);
            double timeDelta = currentTime - startTime;
            double duration = _inOutRectDragStartOutTime - _inOutRectDragStartInTime;
            double total = GetTotalTimeRange();
            double newIn = Math.Clamp(_inOutRectDragStartInTime + timeDelta, 0, total - duration);
            bool smooth = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
            if (!smooth)
                newIn = Transport.SnapToBeat(newIn, Math.Max(1, ViewState.GridSubdivisionsPerBeat));
            newIn = Math.Clamp(newIn, 0, total - duration);
            double newOut = newIn + duration;
            Project.InTime = newIn;
            Project.OutTime = newOut;
        }

        // Right-drag: playhead follows mouse (snap by default, Shift = smooth)
        if (_rightDragSeeking && Input.MouseRightDown && ViewState != null && Transport != null)
        {
            var trackArea = GetTrackContentBounds(content);
            double time = Math.Max(0, ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X));
            bool smooth = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
            if (!smooth)
                time = Transport.SnapToBeat(time, Math.Max(1, ViewState.GridSubdivisionsPerBeat));
            time = ClampSeekTime(time);
            if (SeekRequested != null)
                SeekRequested(time);
            else
                Transport.Seek(time);
            FollowPlayheadIfOutOfView(time);
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
                    CutTrackWithRange(_durationResizeTrack, newStartTime, newStartTime + newDuration, newStartTime);
                    _durationResizeEventTime = newStartTime;
                    if (Selection != null)
                        Selection.SetSingleNote(_durationResizeTrack, newStartTime);
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
                CutTrackWithRange(_durationResizeTrack, _durationResizeEventTime, _durationResizeEventTime + duration, _durationResizeEventTime);
            }
        }

        // Note move: drag note body to new time and/or track. Snap to grid by default; Shift = free-form.
        // Use grab offset so the note stays under the cursor where it was grabbed (no jump to cursor).
        // When multiple notes are selected, move all of them preserving relative positions.
        if (_noteMoveTrack != null && Input.MouseLeftDown && Input.IsDragging && ViewState != null && Transport != null && Project?.EventTracks != null)
        {
            var trackArea = GetTrackContentBounds(content);
            double cursorTime = ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X);
            double newTime = Math.Max(0, cursorTime - _noteMoveGrabOffsetSeconds);
            bool freeForm = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
            if (!freeForm)
                newTime = Transport.SnapToBeat(newTime, Math.Max(1, ViewState.GridSubdivisionsPerBeat));
            newTime = Math.Max(0, newTime);
            int row = _laneScrollOffset + (Input.MousePosition.Y - trackArea.Y) / LaneHeight;
            row = Math.Clamp(row, 0, Project.EventTracks.Count - 1);
            var newTrack = Project.EventTracks[row] as EventTrackBase;
            bool positionChanged = Math.Abs(newTime - _noteMoveEventTime) > 0.0001 || newTrack != _noteMoveTrack;
            if (newTrack != null && positionChanged)
            {
                double duration = _noteMoveTrack.GetDuration(_noteMoveEventTime);
                _noteMoveTrack.EventTimes.Remove(_noteMoveEventTime);
                _noteMoveTrack.EventDurations.Remove(_noteMoveEventTime);
                newTrack.EventTimes.Add(newTime);
                newTrack.SetDuration(newTime, duration);
                newTrack.EventTimes.Sort();
                // Don't cut notes under the moved note until release; they stay intact while dragging
                _noteMoveEventTime = newTime;
                _noteMoveTrack = newTrack;

                // Move other selected notes by the same delta (preserve relative time and row offset)
                if (_noteMoveMultiOthers != null && _noteMoveMultiCurrent != null)
                {
                    int anchorRow = Project.EventTracks.IndexOf(newTrack);
                    if (anchorRow < 0) anchorRow = 0;
                    for (int i = 0; i < _noteMoveMultiOthers.Count && i < _noteMoveMultiCurrent.Count; i++)
                    {
                        var (_, _, dur, rowOffset, timeOffset) = _noteMoveMultiOthers[i];
                        var (curTrack, curTime) = _noteMoveMultiCurrent[i];
                        int newRow = Math.Clamp(anchorRow + rowOffset, 0, Project.EventTracks.Count - 1);
                        var targetTrack = Project.EventTracks[newRow] as EventTrackBase;
                        if (targetTrack == null) continue;
                        double otherNewTime = Math.Max(0, newTime + timeOffset);
                        curTrack.EventTimes.Remove(curTime);
                        curTrack.EventDurations.Remove(curTime);
                        targetTrack.EventTimes.Add(otherNewTime);
                        targetTrack.SetDuration(otherNewTime, dur);
                        targetTrack.EventTimes.Sort();
                        _noteMoveMultiCurrent[i] = (targetTrack, otherNewTime);
                    }
                    if (Selection != null)
                    {
                        var newSelection = new List<(IEventTrack Track, double EventTime)> { (newTrack, newTime) };
                        foreach (var (t, tm) in _noteMoveMultiCurrent)
                            newSelection.Add((t, tm));
                        Selection.SetSelectedNotes(newSelection);
                    }
                }
                else if (Selection != null)
                {
                    Selection.SetSingleNote(newTrack, newTime);
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

        // Horizontal scrollbar thumb edge resize (FL Studio-style): drag left/right edge of thumb to zoom
        if ((_horizontalScrollResizeLeftEdge || _horizontalScrollResizeRightEdge) && Input.MouseLeftDown && ViewState != null)
        {
            var trackArea = GetTrackContentBounds(content);
            var hBar = GetHorizontalScrollbarBounds(content);
            double totalRange = GetTotalTimeRange();
            if (totalRange > 0 && hBar.Width > 0)
            {
                double timeAtMouse = (Input.MousePosition.X - hBar.X) / (double)hBar.Width * totalRange;
                double minVisibleDuration = trackArea.Width / (double)ViewState.MaxZoom;
                // Allow resizing up to full project duration so the scrollbar thumb can cover the whole bar
                double maxVisibleDuration = Math.Min(totalRange, trackArea.Width / (double)ViewState.MinZoom);
                if (_horizontalScrollResizeLeftEdge)
                {
                    double viewEnd = ViewState.ViewStartTime + trackArea.Width / (double)ViewState.Zoom;
                    timeAtMouse = Math.Clamp(timeAtMouse, 0, viewEnd - minVisibleDuration);
                    double newVisibleDuration = viewEnd - timeAtMouse;
                    newVisibleDuration = Math.Clamp(newVisibleDuration, minVisibleDuration, maxVisibleDuration);
                    ViewState.Zoom = (float)(trackArea.Width / newVisibleDuration);
                    ViewState.Zoom = Math.Clamp(ViewState.Zoom, ViewState.MinZoom, ViewState.MaxZoom);
                    ViewState.ViewStartTime = Math.Max(0, Math.Min(totalRange - trackArea.Width / ViewState.Zoom, timeAtMouse));
                }
                else
                {
                    timeAtMouse = Math.Clamp(timeAtMouse, ViewState.ViewStartTime + minVisibleDuration, totalRange);
                    double newVisibleDuration = timeAtMouse - ViewState.ViewStartTime;
                    newVisibleDuration = Math.Clamp(newVisibleDuration, minVisibleDuration, maxVisibleDuration);
                    ViewState.Zoom = (float)(trackArea.Width / newVisibleDuration);
                    ViewState.Zoom = Math.Clamp(ViewState.Zoom, ViewState.MinZoom, ViewState.MaxZoom);
                    double viewEnd = ViewState.ViewStartTime + trackArea.Width / (double)ViewState.Zoom;
                    if (viewEnd > totalRange)
                        ViewState.ViewStartTime = Math.Max(0, totalRange - trackArea.Width / (double)ViewState.Zoom);
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
                int thumbW = Math.Max(MinHorizontalScrollbarThumbWidth, (int)(visibleDuration / totalRange * hScrollBar.Width));
                int travel = hScrollBar.Width - thumbW;
                double maxStart = totalRange - visibleDuration;
                if (travel > 0 && maxStart > 0)
                {
                    int deltaPixel = Input.MousePosition.X - _horizontalScrollDragStartPixel;
                    // Scale so thumb follows mouse 1:1 and we can reach the end (maxStart)
                    double deltaTime = deltaPixel * maxStart / travel;
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

            // Horizontal scrollbar: start drag, edge resize (thumb or track), or click to jump
            var hScrollBar = GetHorizontalScrollbarBounds(content);
            if (Input.MouseLeftPressed && hScrollBar.Contains(Input.MousePosition))
            {
                var thumb = GetHorizontalScrollbarThumbBounds(content);
                if (thumb.Contains(Input.MousePosition))
                {
                    int edgeW = Math.Min(ScrollbarEdgeResizeWidth, Math.Max(0, thumb.Width / 2));
                    var leftEdgeRect = new Rectangle(thumb.X, thumb.Y, edgeW, thumb.Height);
                    var rightEdgeRect = new Rectangle(thumb.Right - edgeW, thumb.Y, edgeW, thumb.Height);
                    if (leftEdgeRect.Contains(Input.MousePosition))
                        _horizontalScrollResizeLeftEdge = true;
                    else if (rightEdgeRect.Contains(Input.MousePosition))
                        _horizontalScrollResizeRightEdge = true;
                    else
                    {
                        _horizontalScrollDragging = true;
                        _horizontalScrollDragStartPixel = Input.MousePosition.X;
                        _horizontalScrollDragStartTime = ViewState!.ViewStartTime;
                    }
                }
                else
                {
                    // Click on track: resize from beginning/end, or jump
                    double totalRange = GetTotalTimeRange();
                    if (totalRange > 0)
                    {
                        if (Input.MousePosition.X < thumb.X)
                            _horizontalScrollResizeLeftEdge = true; // drag from beginning of scrollbar
                        else if (Input.MousePosition.X > thumb.Right)
                            _horizontalScrollResizeRightEdge = true;
                        else
                        {
                            // Click in gap: jump view so click position is at 1/3 from left
                            double visibleDuration = hScrollBar.Width / (double)ViewState!.Zoom;
                            double timeAtClick = (Input.MousePosition.X - hScrollBar.X) / (double)hScrollBar.Width * totalRange;
                            ViewState.ViewStartTime = Math.Max(0, Math.Min(totalRange - visibleDuration, timeAtClick - visibleDuration / 3.0));
                        }
                    }
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
                    // Multiplicative zoom: finer steps when zoomed out, larger when zoomed in (~8% per tick)
                    float zoomFactor = wheel > 0 ? 1.08f : 1f / 1.08f;
                    ViewState.ZoomAt(zoomFactor, Input.MousePosition.X, trackAreaZoom.X);
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
            // I = set In point at playhead (Out = end of song if not set); O = set Out at playhead (In = 0 if not set)
            if (Project != null && Transport != null && (Input.IsKeyPressed(Keys.I) || Input.IsKeyPressed(Keys.O)))
            {
                double t = double.IsFinite(PlayheadDisplayTime) ? PlayheadDisplayTime : Transport.CurrentTime;
                t = Math.Max(0, t);
                if (Input.IsKeyPressed(Keys.I))
                {
                    Project.InTime = t;
                    if (!Project.OutTime.HasValue)
                        Project.OutTime = GetTotalTimeRange();
                    else if (Project.OutTime.Value < t)
                        Project.OutTime = t;
                }
                if (Input.IsKeyPressed(Keys.O))
                {
                    if (!Project.InTime.HasValue)
                        Project.InTime = 0;
                    Project.OutTime = t;
                    if (Project.InTime.Value > t)
                        Project.InTime = t;
                }
            }
            if (Input.IsKeyPressed(Keys.End))
            {
                double total = GetTotalTimeRange();
                var trackArea = GetTrackContentBounds(content);
                double visibleDuration = trackArea.Width / (double)ViewState.Zoom;
                ViewState.ViewStartTime = Math.Max(0, total - visibleDuration);
            }

            // Right click: remove in/out in strip, delete event time if on a block, otherwise seek and start right-drag seeking
            if (Input.MouseRightPressed && Transport != null && ViewState != null)
            {
                if (GetInOutStripBounds(content).Contains(Input.MousePosition) && Project != null)
                {
                    if (CommandStack != null)
                        CommandStack.Execute(new SetInOutCommand(Project, null, null));
                    else
                    {
                        Project.InTime = null;
                        Project.OutTime = null;
                    }
                }
                else
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
                        Selection?.RemoveNoteFromSelection(hitTrack, hitTime.Value);
                    }
                    else
                    {
                        double time = ClampSeekTime(Math.Max(0, ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X)));
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
                    double time = ClampSeekTime(Math.Max(0, ViewState.ScreenToTime(Input.MousePosition.X, trackAreaForSeek.X)));
                    if (SeekRequested != null)
                        SeekRequested(time);
                    else
                        Transport.Seek(time);
                    _rightDragSeeking = true;
                }
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
                else if (GetInOutStripBounds(content).Contains(Input.MousePosition))
                {
                    // In/out strip: only in/out rectangle edges and body (no playhead)
                    double? inT = Project?.InTime;
                    double? outT = Project?.OutTime;
                    if (inT.HasValue && outT.HasValue && ViewState != null)
                    {
                        float inX = ViewState.TimeToScreen(inT.Value, trackArea.X);
                        float outX = ViewState.TimeToScreen(outT.Value, trackArea.X);
                        int leftEdgeLeft = (int)inX;
                        int leftEdgeRight = leftEdgeLeft + InOutEdgeHandleWidth;
                        int rightEdgeLeft = (int)outX - InOutEdgeHandleWidth;
                        int rightEdgeRight = (int)outX;
                        int mx = Input.MousePosition.X;
                        if (mx >= leftEdgeLeft && mx < leftEdgeRight)
                            _inOutResizeLeft = true;
                        else if (mx >= rightEdgeLeft && mx < rightEdgeRight)
                            _inOutResizeRight = true;
                        else if (mx >= leftEdgeRight && mx < rightEdgeLeft)
                        {
                            _inOutRectDragging = true;
                            _inOutRectDragStartInTime = inT.Value;
                            _inOutRectDragStartOutTime = outT.Value;
                            _inOutRectDragStartMouseX = Input.MousePosition.X;
                        }
                    }
                }
                else if (GetPlayheadStripBounds(content).Contains(Input.MousePosition))
                {
                    // Playhead strip only: click/drag to seek (no in/out)
                    _playheadStripDragging = true;
                    double time = Math.Max(0, ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X));
                    bool smooth = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
                    if (!smooth)
                        time = Transport.SnapToBeat(time, Math.Max(1, ViewState.GridSubdivisionsPerBeat));
                    time = ClampSeekTime(time);
                    if (SeekRequested != null)
                        SeekRequested(time);
                    else
                        Transport.Seek(time);
                }
            }

            if (Input.MouseLeftPressed && Project?.EventTracks != null && ViewState != null && Transport != null && !_playheadBulbDragging && !_playheadStripDragging && !_inOutResizeLeft && !_inOutResizeRight && !_inOutRectDragging)
            {
                var trackArea = GetTrackContentBounds(content);
                if (trackArea.Contains(Input.MousePosition))
                {
                    var (hitTrack, hitTime, hitLeftEdge, hitRightEdge) = HitTestEvent(content, Input.MousePosition.X, Input.MousePosition.Y);
                    if (hitTrack != null && hitTime.HasValue && (hitLeftEdge || hitRightEdge) && hitTrack is EventTrackBase resizeTrack)
                    {
                        // Only start resize when user actually drags; until then keep as pending (click = select only)
                        _pendingResizeTrack = resizeTrack;
                        _pendingResizeEventTime = hitTime.Value;
                        _pendingResizeFromLeft = hitLeftEdge;
                        if (Selection != null)
                            Selection.SetSingleNote(hitTrack, hitTime.Value);
                    }
                    else
                    {
                        double time = ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X);
                        double snappedTime = Transport.SnapToBeat(time, ViewState.GridSubdivisionsPerBeat);
                        if (hitTrack != null && hitTime.HasValue)
                        {
                            // Next drawn note will use this note's duration (Ableton/FL-style)
                            _nextNoteDurationSeconds = hitTrack is EventTrackBase bt ? bt.GetDuration(hitTime.Value) : EventTrackConstants.DefaultEventDurationSeconds;
                            // Keep multi-selection when clicking a note that's already selected so drag moves all; otherwise select only this note
                            if (Selection != null)
                            {
                                if (Selection.SelectedNotes.Count <= 1 || !Selection.IsNoteSelected(hitTrack, hitTime.Value))
                                    Selection.SetSingleNote(hitTrack, hitTime.Value);
                            }
                            // Only start move when user actually drags; until then keep as pending (click = select only)
                            if (hitTrack is EventTrackBase moveTrack)
                            {
                                _pendingNoteMoveTrack = moveTrack;
                                _pendingNoteMoveEventTime = hitTime.Value;
                            }
                        }
                        else
                        {
                            // Empty space (no note hit): start pending rectangle selection; add note on release only if no drag and row has a track
                            _pendingRectSelect = true;
                            _rectSelectStart = Input.MousePosition;
                        }
                    }
                }
            }
        }
    }

    /// <summary>Note/grid area, excluding horizontal and vertical scrollbars.</summary>
    private static Rectangle GetTrackContentBounds(Rectangle content)
    {
        int h = Math.Max(0, content.Height - HorizontalScrollbarHeight);
        int w = Math.Max(0, content.Width - ScrollbarWidth);
        return new Rectangle(content.X, content.Y, w, h);
    }

    /// <summary>Horizontal strip above the playhead strip; in/out range rectangle and edge/body drag live here.</summary>
    private static Rectangle GetInOutStripBounds(Rectangle content)
    {
        var trackArea = GetTrackContentBounds(content);
        int playheadStripTop = content.Y - TimelinePlayheadRenderer.PlayheadHeadOffset - TimelinePlayheadRenderer.PlayheadBulbRadius;
        int stripTop = playheadStripTop - InOutStripHeight;
        return new Rectangle(trackArea.X, stripTop, trackArea.Width, InOutStripHeight);
    }

    /// <summary>Horizontal strip above the track area where the playhead head sits; clicking here teleports the playhead.</summary>
    private static Rectangle GetPlayheadStripBounds(Rectangle content)
    {
        var trackArea = GetTrackContentBounds(content);
        int stripTop = content.Y - TimelinePlayheadRenderer.PlayheadHeadOffset - TimelinePlayheadRenderer.PlayheadBulbRadius;
        int stripHeight = TimelinePlayheadRenderer.PlayheadHeadOffset + TimelinePlayheadRenderer.PlayheadBulbRadius;
        return new Rectangle(trackArea.X, stripTop, trackArea.Width, stripHeight);
    }

    /// <summary>Clamp seek time to in/out bounds when both are set; otherwise ensure non-negative.</summary>
    private double ClampSeekTime(double time)
    {
        time = Math.Max(0, time);
        if (Project?.InTime is { } inT && Project?.OutTime is { } outT && outT > inT)
            return Math.Clamp(time, inT, outT);
        return time;
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
        int w = Math.Max(0, content.Width - ScrollbarWidth);
        return new Rectangle(content.X, content.Bottom - HorizontalScrollbarHeight, w, HorizontalScrollbarHeight);
    }

    private Rectangle GetHorizontalScrollbarThumbBounds(Rectangle content)
    {
        if (ViewState == null) return default;
        var bar = GetHorizontalScrollbarBounds(content);
        double total = GetTotalTimeRange();
        double visibleDuration = bar.Width / (double)ViewState.Zoom;
        if (total <= 0 || visibleDuration >= total)
            return new Rectangle(bar.X, bar.Y, bar.Width, bar.Height);
        int thumbW = Math.Max(MinHorizontalScrollbarThumbWidth, (int)(visibleDuration / total * bar.Width));
        double maxStart = total - visibleDuration;
        // Map ViewStartTime in [0, maxStart] to thumb X so thumb reaches the right edge when scrolled to end
        int thumbX = bar.X + (maxStart > 0 ? (int)(ViewState.ViewStartTime / maxStart * (bar.Width - thumbW)) : 0);
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
        // When we have a song or events, timeline length matches content; otherwise use minimum range
        double contentLength = Math.Max(fromWaveform, fromEvents);
        return contentLength > 0 ? contentLength : MinTotalTimeRange;
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

    /// <summary>True if [startTime, startTime+duration] overlaps any event on the track. If excludeEventTime is set, that event is ignored (for move/resize).</summary>
    private bool EventsOverlap(IEventTrack track, double startTime, double duration, double? excludeEventTime)
    {
        double endTime = startTime + duration;
        foreach (var et in track.EventTimes)
        {
            if (excludeEventTime.HasValue && Math.Abs(et - excludeEventTime.Value) < 0.0001)
                continue;
            double d = GetEventDuration(track, et);
            double otherEnd = et + d;
            if (startTime < otherEnd && endTime > et)
                return true;
        }
        return false;
    }

    /// <summary>Shorten or split notes on the track that overlap [cutStart, cutEnd]. The event at excludeEventTime is left unchanged (the "cutter" note).</summary>
    private void CutTrackWithRange(EventTrackBase track, double cutStart, double cutEnd, double? excludeEventTime)
    {
        const double eps = 0.0001;
        var toProcess = new List<(double eventTime, double duration)>();
        foreach (var et in track.EventTimes)
        {
            if (excludeEventTime.HasValue && Math.Abs(et - excludeEventTime.Value) < eps)
                continue;
            double d = track.GetDuration(et);
            double end = et + d;
            if (cutStart >= end - eps || cutEnd <= et + eps) continue;
            toProcess.Add((et, d));
        }
        foreach (var (eventTime, duration) in toProcess)
        {
            double end = eventTime + duration;
            if (cutStart <= eventTime + eps && cutEnd >= end - eps)
            {
                track.EventTimes.Remove(eventTime);
                track.EventDurations.Remove(eventTime);
            }
            else if (cutStart <= eventTime + eps)
            {
                track.EventTimes.Remove(eventTime);
                track.EventDurations.Remove(eventTime);
                double newDur = end - cutEnd;
                if (newDur >= MinNoteDurationSeconds)
                {
                    track.EventTimes.Add(cutEnd);
                    track.SetDuration(cutEnd, newDur);
                }
            }
            else if (cutEnd >= end - eps)
            {
                double newDur = cutStart - eventTime;
                if (newDur >= MinNoteDurationSeconds)
                    track.SetDuration(eventTime, newDur);
                else
                {
                    track.EventTimes.Remove(eventTime);
                    track.EventDurations.Remove(eventTime);
                }
            }
            else
            {
                track.SetDuration(eventTime, cutStart - eventTime);
                double rightDur = end - cutEnd;
                if (rightDur >= MinNoteDurationSeconds)
                {
                    track.EventTimes.Add(cutEnd);
                    track.SetDuration(cutEnd, rightDur);
                }
            }
        }
        track.EventTimes.Sort();
    }

    /// <summary>Returns (row, snappedTime, duration) for the note placement preview, or (row: -1, 0, 0) when no preview should be shown.</summary>
    private (int row, double time, double duration) GetNotePreviewPosition(Rectangle content, Rectangle trackArea)
    {
        if (ViewState == null || Transport == null || Project?.EventTracks == null || Input == null)
            return (-1, 0, 0);
        // Don't show preview while dragging note, resizing, rectangle selecting, or dragging playhead/scrollbars
        if (_noteMoveTrack != null || _durationResizeTrack != null || _rectangleSelecting
            || _playheadBulbDragging || _playheadStripDragging || _horizontalScrollDragging
            || _verticalScrollDragging || _inOutResizeLeft || _inOutResizeRight || _inOutRectDragging)
            return (-1, 0, 0);

        double duration = Math.Max(MinNoteDurationSeconds, _nextNoteDurationSeconds);
        int row;
        double time;

        if (Input.MouseLeftDown && _pendingRectSelect && !_rectangleSelecting)
        {
            // User has clicked empty space; note will be placed at click position on release — show preview there
            if (!trackArea.Contains(_rectSelectStart)) return (-1, 0, 0);
            row = _laneScrollOffset + (_rectSelectStart.Y - trackArea.Y) / LaneHeight;
            if (row < 0 || row >= Project.EventTracks.Count) return (-1, 0, 0);
            time = ViewState.ScreenToTime(_rectSelectStart.X, trackArea.X);
            time = Transport.SnapToBeat(time, ViewState.GridSubdivisionsPerBeat);
            return (row, time, duration);
        }

        if (!Input.MouseLeftDown && trackArea.Contains(Input.MousePosition))
        {
            // Hover over track area — show where a click would place a note (only on empty space)
            var (hitTrack, hitTime, _, _) = HitTestEvent(content, Input.MousePosition.X, Input.MousePosition.Y);
            if (hitTrack != null && hitTime == null)
            {
                row = hitTrack is EventTrackBase bt ? Project.EventTracks.IndexOf(bt) : -1;
                if (row < 0) return (-1, 0, 0);
                time = ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X);
                time = Transport.SnapToBeat(time, ViewState.GridSubdivisionsPerBeat);
                return (row, time, duration);
            }
        }

        return (-1, 0, 0);
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
            bool canResize = blockW >= MinNoteWidthForResize;
            bool leftEdge = canResize && blockW >= NoteResizeHandleWidth && screenX < rect.X + NoteResizeHandleWidth;
            bool rightEdge = canResize && blockW >= NoteResizeHandleWidth && screenX >= rect.Right - NoteResizeHandleWidth;
            if (leftEdge && rightEdge)
                return (track, et, screenX < rect.X + blockW / 2, !(screenX < rect.X + blockW / 2));
            return (track, et, leftEdge, rightEdge);
        }
        return (track, null, false, false);
    }

    /// <summary>Returns a rectangle with positive width/height from two points.</summary>
    private static Rectangle GetNormalizedRect(Point a, Point b)
    {
        int x = Math.Min(a.X, b.X);
        int y = Math.Min(a.Y, b.Y);
        return new Rectangle(x, y, Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
    }

    /// <summary>Returns all (track, eventTime) notes whose screen bounds intersect the given screen rectangle (in content coords).</summary>
    private List<(IEventTrack Track, double EventTime)> GetNotesInScreenRect(Rectangle content, int minX, int minY, int maxX, int maxY)
    {
        var result = new List<(IEventTrack, double)>();
        if (ViewState == null || Project?.EventTracks == null) return result;
        var trackArea = GetTrackContentBounds(content);
        int minRow = (minY - trackArea.Y) / LaneHeight + _laneScrollOffset;
        int maxRow = (maxY - trackArea.Y + LaneHeight - 1) / LaneHeight + _laneScrollOffset;
        minRow = Math.Clamp(minRow, 0, Project.EventTracks.Count - 1);
        maxRow = Math.Clamp(maxRow, 0, Project.EventTracks.Count - 1);
        for (int row = minRow; row <= maxRow; row++)
        {
            int blockY = trackArea.Y + (row - _laneScrollOffset) * LaneHeight + 2;
            int blockH = LaneHeight - 4;
            if (blockY + blockH < minY || blockY > maxY) continue;
            var track = Project.EventTracks[row];
            foreach (var et in track.EventTimes)
            {
                double dur = GetEventDuration(track, et);
                float x = ViewState.TimeToScreen(et, trackArea.X);
                float w = (float)(dur * ViewState.Zoom);
                int blockW = (int)Math.Max(2, w);
                if ((int)x + blockW < minX || (int)x > maxX) continue;
                result.Add((track, et));
            }
        }
        return result;
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

        bool hasEventTracks = Project?.EventTracks != null && Project.EventTracks.Count > 0;

        // Note area background and alternating lane rows — only when we have event tracks
        if (hasEventTracks)
        {
            spriteBatch.Draw(pixel, trackArea, new Color(24, 26, 30));
            for (int row = 0; row * LaneHeight < trackArea.Height; row++)
            {
                if ((row & 1) == 1) continue;
                var rowRect = new Rectangle(trackArea.X, trackArea.Y + row * LaneHeight, trackArea.Width, LaneHeight);
                if (rowRect.Bottom > trackArea.Bottom) rowRect.Height = trackArea.Bottom - rowRect.Y;
                spriteBatch.Draw(pixel, rowRect, new Color(30, 32, 36));
            }
        }

        // Waveform as background of the piano roll (behind grid and notes, on top of lane stripes)
        if (Waveform != null && Waveform.BucketCount > 0)
            WaveformRenderer.Draw(spriteBatch, pixel, trackArea, Waveform, ViewState, trackArea.X);

        // Grid (note area only) — only when we have event tracks
        if (hasEventTracks)
        {
            double bpm = Transport?.BPM ?? Project?.BPM ?? 120;
            int num = Project?.TimeSignatureNumerator ?? 4;
            int den = Project?.TimeSignatureDenominator ?? 4;
            double beatStart = Project?.InTime ?? 0;
            TimelineGridRenderer.Draw(spriteBatch, pixel, trackArea, ViewState, bpm, num, den, beatStart);
        }

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
                var baseTrack = track as EventTrackBase;
                Color trackColor = baseTrack?.TrackColor ?? new Color(120, 200, 255);
                Color borderTint = Darken(trackColor, 0.82f);
                Color handleColor = Darken(trackColor, 0.65f);
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
                    bool selected = Selection != null && Selection.IsNoteSelected(track, eventTime);
                    Color fillTint = selected ? Lighten(trackColor, 1.35f) : trackColor;
                    Color selBorderTint = selected ? NoteSelectionOutline : borderTint;
                    fillTex = selected ? s_noteSelectedFillTexture : s_noteFillTexture;
                    borderTex = selected ? s_noteSelectedBorderTexture : s_noteBorderTexture;
                    int x = (int)fx;
                    // Selected: draw outer white outline first so selection is obvious
                    if (selected)
                        DrawRoundedRect9Slice(spriteBatch, borderTex, x - 2, y - 2, blockW + 4, h + 4, NoteSelectionOutline);
                    // FL Studio style: 9-slice rounded rect so corners stay perfectly circular at any note size (pill-shaped ends when long)
                    DrawRoundedRect9Slice(spriteBatch, borderTex, x - 1, y - 1, blockW + 2, h + 2, selBorderTint);
                    DrawRoundedRect9Slice(spriteBatch, fillTex, x, y, blockW, h, fillTint);
                    // Resize handles: pill-shaped with rounded ends when note is wide enough (zoomed in)
                    if (blockW >= MinNoteWidthForResize && blockW >= NoteResizeHandleWidth && h > NoteCornerRadius * 2 && s_resizeHandlePillTexture != null)
                    {
                        int handleW = Math.Min(5, Math.Max(3, blockW / 5));
                        int inset = NoteCornerRadius;
                        int handleH = h - inset * 2;
                        var leftHandleRect = new Rectangle(x, y + inset, handleW, handleH);
                        var rightHandleRect = new Rectangle(x + blockW - handleW, y + inset, handleW, handleH);
                        spriteBatch.Draw(s_resizeHandlePillTexture, leftHandleRect, new Rectangle(0, 0, ResizeHandlePillWidth, ResizeHandlePillHeight), handleColor);
                        spriteBatch.Draw(s_resizeHandlePillTexture, rightHandleRect, new Rectangle(0, 0, ResizeHandlePillWidth, ResizeHandlePillHeight), handleColor);
                    }
                }
            }
        }

        // Note placement preview: transparent note showing where a click would place a new note
        if (hasEventTracks && ViewState != null && Transport != null && Project?.EventTracks != null
            && s_noteFillTexture != null && s_noteBorderTexture != null)
        {
            var (previewRow, previewTime, previewDuration) = GetNotePreviewPosition(content, trackArea);
            if (previewRow >= 0 && previewRow < Project.EventTracks.Count && previewDuration >= MinNoteDurationSeconds)
            {
                if (previewTime + previewDuration >= ViewState.ViewStartTime && previewTime <= viewEnd)
                {
                    int visibleRowIndex = previewRow - _laneScrollOffset;
                    if (visibleRowIndex >= 0 && visibleRowIndex < visibleLanes)
                    {
                        float fx = ViewState.TimeToScreen(previewTime, trackArea.X);
                        float w = (float)(previewDuration * ViewState.Zoom);
                        int y = trackArea.Y + visibleRowIndex * LaneHeight + 2;
                        int h = LaneHeight - 4;
                        int blockW = (int)Math.Max(2, w);
                        int x = (int)fx;
                        var track = Project.EventTracks[previewRow];
                        var baseTrack = track as EventTrackBase;
                        Color trackColor = baseTrack?.TrackColor ?? new Color(120, 200, 255);
                        Color borderTint = Darken(trackColor, 0.82f);
                        const byte ghostFillAlpha = 55;
                        const byte ghostBorderAlpha = 95;
                        Color fillTint = new Color(trackColor.R, trackColor.G, trackColor.B, ghostFillAlpha);
                        Color borderPreview = new Color(borderTint.R, borderTint.G, borderTint.B, ghostBorderAlpha);
                        DrawRoundedRect9Slice(spriteBatch, s_noteBorderTexture, x - 1, y - 1, blockW + 2, h + 2, borderPreview);
                        DrawRoundedRect9Slice(spriteBatch, s_noteFillTexture, x, y, blockW, h, fillTint);
                    }
                }
            }
        }

        // Gray overlay: dim timeline outside in/out range (song boundaries)
        double? inT = Project?.InTime;
        double? outT = Project?.OutTime;
        if (inT.HasValue && outT.HasValue && outT.Value > inT.Value)
        {
            float inX = ViewState.TimeToScreen(inT.Value, trackArea.X);
            float outX = ViewState.TimeToScreen(outT.Value, trackArea.X);
            var dimColor = new Color(0, 0, 0, 160);
            int leftW = (int)inX - trackArea.X;
            if (leftW > 0)
                spriteBatch.Draw(pixel, new Rectangle(trackArea.X, trackArea.Y, leftW, trackArea.Height), dimColor);
            int rightX = (int)outX;
            if (trackArea.Right > rightX)
                spriteBatch.Draw(pixel, new Rectangle(rightX, trackArea.Y, trackArea.Right - rightX, trackArea.Height), dimColor);
        }

        // In/out strip (own line above playhead): background then in/out rectangle and gizmos
        var inOutStripBounds = GetInOutStripBounds(content);
        spriteBatch.Draw(pixel, inOutStripBounds, new Color(38, 42, 48));
        if (inT.HasValue && outT.HasValue && outT.Value > inT.Value)
        {
            float stripInX = ViewState.TimeToScreen(inT.Value, trackArea.X);
            float stripOutX = ViewState.TimeToScreen(outT.Value, trackArea.X);
            int rectX = (int)stripInX;
            int rectW = Math.Max(1, (int)stripOutX - rectX);
            var inOutColor = new Color(70, 130, 180, 180);
            spriteBatch.Draw(pixel, new Rectangle(rectX, inOutStripBounds.Y, rectW, inOutStripBounds.Height), inOutColor);
            // Gizmos: edge grips (In / Out) and center grip (move range)
            var gripColor = new Color(40, 75, 110, 220);
            int gripTop = inOutStripBounds.Y + 3;
            int gripH = Math.Max(2, inOutStripBounds.Height - 6);
            int leftGripX = rectX + 2;
            spriteBatch.Draw(pixel, new Rectangle(leftGripX, gripTop, 1, gripH), gripColor);
            spriteBatch.Draw(pixel, new Rectangle(leftGripX + 2, gripTop, 1, gripH), gripColor);
            int rightGripX = rectX + rectW - 3;
            spriteBatch.Draw(pixel, new Rectangle(rightGripX - 2, gripTop, 1, gripH), gripColor);
            spriteBatch.Draw(pixel, new Rectangle(rightGripX, gripTop, 1, gripH), gripColor);
            if (rectW >= 2 * InOutEdgeHandleWidth + 12)
            {
                int centerX = rectX + rectW / 2;
                spriteBatch.Draw(pixel, new Rectangle(centerX - 4, gripTop, 1, gripH), gripColor);
                spriteBatch.Draw(pixel, new Rectangle(centerX + 3, gripTop, 1, gripH), gripColor);
            }
            var borderColor = new Color(90, 140, 190, 200);
            int b = 1;
            spriteBatch.Draw(pixel, new Rectangle(rectX, inOutStripBounds.Y, rectW, b), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(rectX, inOutStripBounds.Bottom - b, rectW, b), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(rectX, inOutStripBounds.Y, b, inOutStripBounds.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(rectX + rectW - b, inOutStripBounds.Y, b, inOutStripBounds.Height), borderColor);
        }
        // Playhead strip background (so it reads as its own line below in/out)
        var playheadStripBounds = GetPlayheadStripBounds(content);
        spriteBatch.Draw(pixel, playheadStripBounds, new Color(32, 35, 40));

        // Selection rectangle (while user is dragging to select multiple notes)
        if (_rectangleSelecting && Input?.DragStart is { } dragStart)
        {
            var rect = GetNormalizedRect(_rectSelectStart, Input.MousePosition);
            var selRectColor = new Color(70, 130, 180, 80);
            spriteBatch.Draw(pixel, rect, selRectColor);
            var selBorderColor = new Color(70, 130, 180, 200);
            int border = 1;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, border), selBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - border, rect.Width, border), selBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, border, rect.Height), selBorderColor);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - border, rect.Y, border, rect.Height), selBorderColor);
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
            // Grip: two lines to show thumb is grabbable
            if (thumbHeight >= 10)
            {
                var gripColor = new Color(45, 50, 58);
                int gripLeft = thumb.X + 2;
                int gripW = Math.Max(1, thumb.Width - 4);
                int centerY = thumb.Y + thumb.Height / 2;
                spriteBatch.Draw(pixel, new Rectangle(gripLeft, centerY - 2, gripW, 1), gripColor);
                spriteBatch.Draw(pixel, new Rectangle(gripLeft, centerY + 2, gripW, 1), gripColor);
            }
        }

        // Horizontal scrollbar (time / FL Studio style)
        var hBar = GetHorizontalScrollbarBounds(content);
        spriteBatch.Draw(pixel, hBar, new Color(45, 48, 55));
        var hThumb = GetHorizontalScrollbarThumbBounds(content);
        if (hThumb.Width > 0)
        {
            spriteBatch.Draw(pixel, hThumb, new Color(90, 95, 105));
            var gripColor = new Color(45, 50, 58);
            int gripTop = hThumb.Y + 4;
            int gripH = Math.Max(2, hThumb.Height - 8);
            // Resize grip gizmos at left and right edges (only if thumb is wide enough)
            if (hThumb.Width >= 2 * ScrollbarEdgeResizeWidth)
            {
                int leftX = hThumb.X + 3;
                int rightX = hThumb.Right - 4;
                spriteBatch.Draw(pixel, new Rectangle(leftX, gripTop, 1, gripH), gripColor);
                spriteBatch.Draw(pixel, new Rectangle(leftX + 3, gripTop, 1, gripH), gripColor);
                spriteBatch.Draw(pixel, new Rectangle(rightX - 3, gripTop, 1, gripH), gripColor);
                spriteBatch.Draw(pixel, new Rectangle(rightX, gripTop, 1, gripH), gripColor);
            }
            // Center grip: two lines to show thumb is grabbable to move
            if (hThumb.Width >= 12)
            {
                int centerX = hThumb.X + hThumb.Width / 2;
                spriteBatch.Draw(pixel, new Rectangle(centerX - 4, gripTop, 1, gripH), gripColor);
                spriteBatch.Draw(pixel, new Rectangle(centerX + 3, gripTop, 1, gripH), gripColor);
            }
        }
    }
}
