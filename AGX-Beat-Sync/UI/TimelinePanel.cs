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

namespace AGX_Beat_Sync.UI;

public class TimelinePanel : PanelBase
{
    private const int LaneHeight = 28;
    private const int DefaultLaneCount = 4;
    private const double NoteDisplayDuration = 0.25; // seconds, width of one note
    private const int HorizontalScrollbarHeight = 14;
    private const int ScrollbarWidth = 12;
    private const int MinScrollbarThumbHeight = 24;
    private const double MinTotalTimeRange = 60.0;
    /// <summary>FL Studio-style piano key strip width on the left.</summary>
    private const int PianoStripWidth = 44;
    private const int OffsetStripHeight = 20;
    private const int OffsetArrowWidth = 22;
    private const int OffsetNudgeWidth = 20;
    private const int OffsetValueWidth = 56;

    /// <summary>First visible lane index for vertical scroll.</summary>
    private int _laneScrollOffset;
    /// <summary>Note being dragged (anchor when multi-dragging); null when not dragging.</summary>
    private NoteEvent? _draggedNote;
    private NoteTrack? _draggedNoteTrack;
    private double _dragStartTime;
    private int _dragStartLane;
    /// <summary>When set, we're moving multiple notes; each (note, startTime, startLane).</summary>
    private List<(NoteEvent note, double startTime, int startLane)>? _multiDragNotes;
    /// <summary>Horizontal scrollbar: dragging the time thumb.</summary>
    private bool _horizontalScrollDragging;
    private int _horizontalScrollDragStartPixel;
    private double _horizontalScrollDragStartTime;

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
    /// <summary>When set, called when user changes beat offset (grid alignment). Host should set Project.BeatOffsetSeconds and Transport.BeatOffsetSeconds.</summary>
    public Action<double>? OffsetChanged { get; set; }
    /// <summary>When set, called when user clicks to type beat offset (e.g. open time dialog). Host reads Project.BeatOffsetSeconds and on OK sets new value and calls OffsetChanged.</summary>
    public Action? OffsetEditRequested { get; set; }

    private bool _offsetPanelExpanded;
    private Texture2D? _offsetValueTexture;
    private string _offsetCachedString = "";
    private int _offsetCachedW = -1;
    private int _offsetCachedH = -1;

    /// <summary>FL Studio-style dark background for the piano roll area.</summary>
    private static readonly Color PianoRollBackground = new(28, 30, 34);
    /// <summary>Piano strip: lighter row (like white key).</summary>
    private static readonly Color PianoStripLight = new(52, 55, 62);
    /// <summary>Piano strip: darker row (like black key).</summary>
    private static readonly Color PianoStripDark = new(38, 40, 46);
    /// <summary>Note fill (FL default orange/coral).</summary>
    private static readonly Color NoteFill = new(230, 130, 90);
    /// <summary>Note border for definition.</summary>
    private static readonly Color NoteBorder = new(160, 85, 55);
    /// <summary>Selected note fill.</summary>
    private static readonly Color NoteSelectedFill = new(255, 180, 120);
    /// <summary>Selected note border.</summary>
    private static readonly Color NoteSelectedBorder = new(220, 140, 90);

    public TimelinePanel()
    {
        Title = "Timeline";
        BackgroundColor = PianoRollBackground;
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

        FollowPlayheadIfOutOfView();

        var content = ContentBounds;

        // End note drag and horizontal scroll drag on left release (anywhere)
        if (Input.MouseLeftReleased)
        {
            if (_draggedNote != null && _draggedNoteTrack != null && CommandStack != null)
            {
                if (_multiDragNotes != null)
                {
                    foreach (var (note, startTime, startLane) in _multiDragNotes)
                    {
                        if (note.Time != startTime || note.Lane != startLane)
                            CommandStack.Execute(new ModifyNoteCommand(_draggedNoteTrack, note, startTime, startLane, note.Time, note.Lane));
                    }
                }
                else if (_draggedNote.Time != _dragStartTime || _draggedNote.Lane != _dragStartLane)
                {
                    CommandStack.Execute(new ModifyNoteCommand(
                        _draggedNoteTrack, _draggedNote,
                        _dragStartTime, _dragStartLane,
                        _draggedNote.Time, _draggedNote.Lane));
                }
            }
            _draggedNote = null;
            _draggedNoteTrack = null;
            _multiDragNotes = null;
            _horizontalScrollDragging = false;
        }

        // Update note position while dragging (runs even if mouse leaves panel)
        if (_draggedNote != null && _draggedNoteTrack != null && Input.MouseLeftDown && ViewState != null && Transport != null)
        {
            var trackArea = GetTrackContentBounds(content);
            double time = ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X);
            int gridDiv = ViewState?.GridSubdivisionsPerBeat ?? 4;
            time = Math.Max(0, Transport.SnapToBeat(time, gridDiv));
            int lane = trackArea.Contains(Input.MousePosition)
                ? _laneScrollOffset + (Input.MousePosition.Y - trackArea.Y) / LaneHeight
                : _draggedNote.Lane;
            if (lane < 0) lane = 0;
            if (_multiDragNotes != null)
            {
                double deltaTime = time - _dragStartTime;
                int deltaLane = lane - _dragStartLane;
                foreach (var (note, startTime, startLane) in _multiDragNotes)
                {
                    note.Time = Math.Max(0, Transport.SnapToBeat(startTime + deltaTime, ViewState?.GridSubdivisionsPerBeat ?? 4));
                    note.Lane = Math.Max(0, startLane + deltaLane);
                }
            }
            else
            {
                _draggedNote.Time = time;
                _draggedNote.Lane = lane;
            }
            _draggedNoteTrack.Notes.Sort((a, b) => a.Time.CompareTo(b.Time));
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

        if (ContainsPoint(Input.MousePosition))
        {
            var arrowRect = GetOffsetArrowBounds(content);
            bool inOffsetStrip = arrowRect.Contains(Input.MousePosition)
                || (_offsetPanelExpanded && (GetOffsetValueBounds(content).Contains(Input.MousePosition)
                    || GetOffsetMinusBounds(content).Contains(Input.MousePosition)
                    || GetOffsetPlusBounds(content).Contains(Input.MousePosition)));

            if (Input.MouseLeftPressed && inOffsetStrip && Project != null && OffsetChanged != null)
            {
                if (arrowRect.Contains(Input.MousePosition))
                {
                    _offsetPanelExpanded = !_offsetPanelExpanded;
                }
                else if (_offsetPanelExpanded)
                {
                    if (GetOffsetMinusBounds(content).Contains(Input.MousePosition))
                    {
                        double step = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift) ? 0.01 : 0.1;
                        double newOffset = Math.Max(0, Project.BeatOffsetSeconds - step);
                        OffsetChanged(newOffset);
                    }
                    else if (GetOffsetPlusBounds(content).Contains(Input.MousePosition))
                    {
                        double step = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift) ? 0.01 : 0.1;
                        double newOffset = Project.BeatOffsetSeconds + step;
                        OffsetChanged(newOffset);
                    }
                    else if (GetOffsetValueBounds(content).Contains(Input.MousePosition))
                    {
                        OffsetEditRequested?.Invoke();
                    }
                }
            }
            else if (!inOffsetStrip)
            {
            // Pan with middle mouse drag
            if (Input.IsDragging && Input.MouseMiddleDown)
            {
                ViewState.Pan(-Input.DragDelta.X);
                ClampViewToTimeRange(content);
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

            // Right click: delete note if on a note, otherwise seek playhead
            if (Input.MouseRightPressed && Transport != null)
            {
                var trackArea = GetTrackContentBounds(content);
                if (trackArea.Contains(Input.MousePosition) && Project?.NoteTracks.Count > 0)
                {
                    var track = Project.NoteTracks[0];
                    var hit = HitTestNote(track, content, Input.MousePosition.X, Input.MousePosition.Y);
                    if (hit != null && CommandStack != null)
                    {
                        CommandStack.Execute(new DeleteNoteCommand(track, hit));
                        if (Selection != null && Selection.IsSelected(hit))
                            Selection.SetSingle(null, null);
                    }
                    else
                    {
                        double time = Math.Max(0, ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X));
                        if (SeekRequested != null)
                            SeekRequested(time);
                        else
                            Transport.Seek(time);
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
                }
            }

            // Left click: start dragging a note, or place/select note
            if (Input.MouseLeftPressed && Project?.NoteTracks.Count > 0 && Transport != null)
            {
                var trackArea = GetTrackContentBounds(content);
                if (trackArea.Contains(Input.MousePosition))
                {
                    double time = ViewState.ScreenToTime(Input.MousePosition.X, trackArea.X);
                    int lane = _laneScrollOffset + (Input.MousePosition.Y - trackArea.Y) / LaneHeight;
                    if (lane < 0) lane = 0;

                    double snappedTime = Transport.SnapToBeat(time, ViewState?.GridSubdivisionsPerBeat ?? 4);
                    var track = Project.NoteTracks[0];
                    var hit = HitTestNote(track, content, Input.MousePosition.X, Input.MousePosition.Y);
                    if (hit != null)
                    {
                        _draggedNote = hit;
                        _draggedNoteTrack = track;
                        _dragStartTime = hit.Time;
                        _dragStartLane = hit.Lane;
                        if (Selection != null && Selection.SelectedNoteTrack == track && Selection.SelectedNotes.Count > 1 && Selection.IsSelected(hit))
                        {
                            _multiDragNotes = Selection.SelectedNotes.Select(n => (n, n.Time, n.Lane)).ToList();
                        }
                        else
                        {
                            _multiDragNotes = null;
                            if (Selection != null)
                                Selection.SetSingle(hit, track);
                        }
                    }
                    else if (Selection != null && CommandStack != null)
                    {
                        var note = new NoteEvent { Time = snappedTime, Lane = lane, Type = 0 };
                        CommandStack.Execute(new AddNoteCommand(track, note));
                        Selection.SetSingle(note, track);
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

    private Rectangle GetOffsetArrowBounds(Rectangle content)
    {
        var track = GetTrackContentBounds(content);
        return new Rectangle(track.X, track.Y, OffsetArrowWidth, OffsetStripHeight);
    }

    private Rectangle GetOffsetValueBounds(Rectangle content)
    {
        var track = GetTrackContentBounds(content);
        return new Rectangle(track.X + OffsetArrowWidth + 2, track.Y, OffsetValueWidth, OffsetStripHeight);
    }

    private Rectangle GetOffsetMinusBounds(Rectangle content)
    {
        var track = GetTrackContentBounds(content);
        return new Rectangle(track.X + OffsetArrowWidth + 2 + OffsetValueWidth, track.Y, OffsetNudgeWidth, OffsetStripHeight);
    }

    private Rectangle GetOffsetPlusBounds(Rectangle content)
    {
        var track = GetTrackContentBounds(content);
        return new Rectangle(track.X + OffsetArrowWidth + 2 + OffsetValueWidth + OffsetNudgeWidth, track.Y, OffsetNudgeWidth, OffsetStripHeight);
    }

    private static Rectangle GetScrollbarBounds(Rectangle content)
    {
        int trackH = Math.Max(0, content.Height - HorizontalScrollbarHeight);
        return new Rectangle(content.Right - ScrollbarWidth, content.Y, ScrollbarWidth, trackH);
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
        double fromNotes = 0;
        if (Project?.NoteTracks.Count > 0)
        {
            var track = Project.NoteTracks[0];
            if (track.Notes.Count > 0)
                fromNotes = track.Notes.Max(n => n.Time) + 10;
        }
        return Math.Max(MinTotalTimeRange, Math.Max(fromWaveform, fromNotes));
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
        if (Project == null || Project.NoteTracks.Count == 0) return DefaultLaneCount;
        var track = Project.NoteTracks[0];
        if (track.Notes.Count == 0) return DefaultLaneCount;
        return Math.Max(DefaultLaneCount, track.Notes.Max(n => n.Lane) + 1);
    }

    private void ClampLaneScroll(int visibleLanes, int totalLanes)
    {
        int maxOffset = Math.Max(0, totalLanes - visibleLanes);
        _laneScrollOffset = Math.Clamp(_laneScrollOffset, 0, maxOffset);
    }

    private NoteEvent? HitTestNote(NoteTrack track, Rectangle content, int screenX, int screenY)
    {
        if (ViewState == null) return null;
        var trackArea = GetTrackContentBounds(content);
        if (!trackArea.Contains(screenX, screenY)) return null;
        int lane = _laneScrollOffset + (screenY - trackArea.Y) / LaneHeight;
        foreach (var note in track.Notes)
        {
            if (note.Lane != lane) continue;
            float x = ViewState.TimeToScreen(note.Time, trackArea.X);
            float w = (float)(NoteDisplayDuration * ViewState.Zoom);
            int noteY = trackArea.Y + (note.Lane - _laneScrollOffset) * LaneHeight + 2;
            var rect = new Rectangle((int)x, noteY, (int)Math.Max(2, w), LaneHeight - 4);
            if (rect.Contains(screenX, screenY))
                return note;
        }
        return null;
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

        // Playhead (use smoothed display time from game to reduce audio position jitter)
        if (Transport != null)
        {
            double t = double.IsFinite(PlayheadDisplayTime) ? PlayheadDisplayTime : Transport.CurrentTime;
            float playheadX = ViewState.TimeToScreen(t, trackArea.X);
            TimelinePlayheadRenderer.Draw(spriteBatch, pixel, content, playheadX);
        }

        // Notes (first note track only) — FL Studio style: orange/coral fill with darker border
        int visibleLanes = GetVisibleLaneCount(trackArea);
        int totalLanes = GetTotalLaneCount();
        if (Project?.NoteTracks.Count > 0)
        {
            var track = Project.NoteTracks[0];
            double viewEnd = ViewState.ViewEndTime(trackArea.Width);

            bool hasSelection = Selection != null;
            foreach (var note in track.Notes)
            {
                int visibleLaneIndex = note.Lane - _laneScrollOffset;
                if (visibleLaneIndex < 0 || visibleLaneIndex >= visibleLanes)
                    continue;
                if (note.Time + NoteDisplayDuration < ViewState.ViewStartTime || note.Time > viewEnd)
                    continue;
                float x = ViewState.TimeToScreen(note.Time, trackArea.X);
                float w = (float)(NoteDisplayDuration * ViewState.Zoom);
                int y = trackArea.Y + visibleLaneIndex * LaneHeight + 2;
                int h = LaneHeight - 4;
                int noteW = (int)Math.Max(2, w);
                bool selected = hasSelection && Selection!.IsSelected(note);
                var fill = selected ? NoteSelectedFill : NoteFill;
                var border = selected ? NoteSelectedBorder : NoteBorder;
                // Border (FL-style outline)
                spriteBatch.Draw(pixel, new Rectangle((int)x - 1, y - 1, noteW + 2, h + 2), border);
                spriteBatch.Draw(pixel, new Rectangle((int)x, y, noteW, h), fill);
            }
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

        // Beat offset strip (top-left of track: arrow expands to show offset alignment)
        DrawOffsetStrip(spriteBatch, pixel, content);
    }

    private void DrawOffsetStrip(SpriteBatch spriteBatch, Texture2D pixel, Rectangle content)
    {
        var arrowRect = GetOffsetArrowBounds(content);
        var stripBg = new Color(38, 41, 46);
        var buttonBg = new Color(58, 62, 70);
        var arrowColor = new Color(180, 185, 195);

        spriteBatch.Draw(pixel, arrowRect, stripBg);
        // Arrow: ">" when collapsed (point at right), "v" when expanded (point at bottom)
        int cx = arrowRect.X + arrowRect.Width / 2;
        int cy = arrowRect.Y + arrowRect.Height / 2;
        if (_offsetPanelExpanded)
        {
            // Down-pointing "v": two legs meeting at (cx, cy+3)
            for (int d = 0; d <= 5; d++)
            {
                int xL = cx - 4 + d;
                int yL = cy - 3 + (d * 6 + 4) / 5;
                spriteBatch.Draw(pixel, new Rectangle(xL, yL, 1, 1), arrowColor);
                int xR = cx + 4 - d;
                int yR = cy - 3 + (d * 6 + 4) / 5;
                spriteBatch.Draw(pixel, new Rectangle(xR, yR, 1, 1), arrowColor);
            }
            spriteBatch.Draw(pixel, new Rectangle(cx, cy + 3, 1, 1), arrowColor);
        }
        else
        {
            // Right-pointing ">": two legs meeting at (cx, cy)
            for (int d = 0; d <= 5; d++)
            {
                int x = cx - 5 + d;
                int yTop = cy - 3 + (d * 3 + 2) / 5;
                int yBot = cy + 3 - (d * 3 + 2) / 5;
                spriteBatch.Draw(pixel, new Rectangle(x, yTop, 1, 1), arrowColor);
                if (yBot != yTop)
                    spriteBatch.Draw(pixel, new Rectangle(x, yBot, 1, 1), arrowColor);
            }
            spriteBatch.Draw(pixel, new Rectangle(cx, cy, 1, 1), arrowColor);
        }

        if (_offsetPanelExpanded)
        {
            var valueRect = GetOffsetValueBounds(content);
            var minusRect = GetOffsetMinusBounds(content);
            var plusRect = GetOffsetPlusBounds(content);
            spriteBatch.Draw(pixel, valueRect, new Color(48, 51, 56));
            spriteBatch.Draw(pixel, minusRect, buttonBg);
            spriteBatch.Draw(pixel, plusRect, buttonBg);
            // Minus: horizontal line
            spriteBatch.Draw(pixel, new Rectangle(minusRect.Center.X - 4, minusRect.Center.Y - 1, 8, 2), arrowColor);
            // Plus: horizontal and vertical
            spriteBatch.Draw(pixel, new Rectangle(plusRect.Center.X - 4, plusRect.Center.Y - 1, 8, 2), arrowColor);
            spriteBatch.Draw(pixel, new Rectangle(plusRect.Center.X - 1, plusRect.Center.Y - 4, 2, 8), arrowColor);
            // Value text
            double offsetSec = Project?.BeatOffsetSeconds ?? 0;
            string offsetStr = offsetSec.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "s";
            EnsureOffsetValueTexture(spriteBatch.GraphicsDevice, offsetStr, valueRect);
            if (_offsetValueTexture != null)
            {
                int tx = valueRect.X + (valueRect.Width - _offsetValueTexture.Width) / 2;
                int ty = valueRect.Y + (valueRect.Height - _offsetValueTexture.Height) / 2;
                spriteBatch.Draw(_offsetValueTexture, new Rectangle(tx, ty, _offsetValueTexture.Width, _offsetValueTexture.Height), Color.White);
            }
        }
    }

    private void EnsureOffsetValueTexture(GraphicsDevice device, string text, Rectangle destRect)
    {
        int w = Math.Max(1, destRect.Width);
        int h = Math.Max(1, destRect.Height);
        if (_offsetCachedString == text && _offsetCachedW == w && _offsetCachedH == h && _offsetValueTexture != null && !_offsetValueTexture.IsDisposed)
            return;
        _offsetCachedString = text;
        _offsetCachedW = w;
        _offsetCachedH = h;
        _offsetValueTexture?.Dispose();
        _offsetValueTexture = CreateOffsetLabelTexture(device, text, w, h);
    }

    private static Texture2D? CreateOffsetLabelTexture(GraphicsDevice device, string text, int width, int height)
    {
        try
        {
            int fontSize = Math.Max(8, Math.Min(14, height * 3 / 4));
            using var font = new Font("Segoe UI", fontSize, FontStyle.Regular);
            using var bitmap = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.DrawString(text, font, System.Drawing.Brushes.White, 0, 0);
            }

            var data = new Microsoft.Xna.Framework.Color[width * height];
            var rect = new System.Drawing.Rectangle(0, 0, width, height);
            var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int byteCount = Math.Abs(bmpData.Stride) * height;
                var rawBytes = new byte[byteCount];
                Marshal.Copy(bmpData.Scan0, rawBytes, 0, byteCount);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        int off = y * bmpData.Stride + x * 4;
                        data[i] = new Microsoft.Xna.Framework.Color(rawBytes[off + 2], rawBytes[off + 1], rawBytes[off], rawBytes[off + 3]);
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }

            var tex = new Texture2D(device, width, height);
            tex.SetData(data);
            return tex;
        }
        catch
        {
            return null;
        }
    }
}
