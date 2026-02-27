using System.Threading;
using System.Windows.Forms;
using AGX_Beat_Sync.Audio;
using AGX_Beat_Sync.Commands;
using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Editor;
using AGX_Beat_Sync.Input;
using AGX_Beat_Sync.Persistence;
using AGX_Beat_Sync.Services;
using AGX_Beat_Sync.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AGX_Beat_Sync;

/// <summary>
/// Main MonoGame entry. Uses fixed timestep for deterministic timing.
/// Renders main UI panels: Transport, Timeline, Inspector, Game View.
/// </summary>
public class BeatSyncGame : Game
{
    private GraphicsDeviceManager _graphics = null!;
    private SpriteBatch _spriteBatch = null!;
    private PanelLayout _layout = null!;
    private TransportBarPanel _transportBar = null!;
    private TimelinePanel _timelinePanel = null!;
    private InspectorPanel _inspectorPanel = null!;
    private GameViewPanel _gameViewPanel = null!;

    public Project Project { get; }
    public Transport Transport { get; }
    public InputManager Input { get; } = new();
    public TimelineViewState TimelineView { get; } = new();
    public EditorSelection Selection { get; } = new();
    public CommandStack CommandStack { get; } = new();
    private AudioPlayback _audio = new();
    private WaveformCache _waveformCache = new();
    private string? _currentProjectPath;
    /// <summary>Smoothed playhead time for drawing (reduces jitter from discrete audio position updates).</summary>
    private double _playheadDisplayTime;
    /// <summary>Dropped file paths queued from FileDrop event (may be raised on another thread).</summary>
    private readonly List<string> _pendingDroppedFiles = new();
    /// <summary>If true, load Project.AudioFilePath on first Update (saved session had a song).</summary>
    private bool _loadSavedAudioOnStart;
    /// <summary>Clipboard for copy/paste: (time offset from anchor, lane, type).</summary>
    private readonly List<(double timeOffset, int lane, int type)> _noteClipboard = new();

    private readonly AudioLoadCoordinator _audioLoad = new();

    private bool _gameViewResizeDragging;
    private int _gameViewResizeStartY;
    private int _gameViewResizeStartHeight;

    public BeatSyncGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // Fixed timestep: 60 updates per second for deterministic logic
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromTicks(166667); // 60 FPS

        Project = new Project();
        Transport = new Transport { BPM = Project.BPM };
        if (ProjectPersistence.TryLoad(out var saved) && saved != null)
        {
            ProjectPersistence.ApplyState(saved, Project, Transport);
            EnsureDefaultTracks(); // ensure at least one track if saved had none
            if (!string.IsNullOrWhiteSpace(Project.AudioFilePath))
                _loadSavedAudioOnStart = true;
        }
        else
            EnsureDefaultTracks();
    }

    private void ClearSelectionIfNoteRemoved()
    {
        if (Selection.SelectedNoteTrack == null) return;
        var track = Selection.SelectedNoteTrack;
        Selection.SelectedNotes.RemoveAll(n => !track.Notes.Contains(n));
        if (Selection.SelectedNotes.Count == 0)
            Selection.Clear();
        else
            Selection.SelectedNote = Selection.SelectedNotes[0];
    }

    private void EnsureDefaultTracks()
    {
        if (Project.NoteTracks.Count == 0)
        {
            var track = new NoteTrack { Name = "Notes" };
            track.Notes.Add(new NoteEvent { Time = 1.0, Lane = 0, Type = 0 });
            track.Notes.Add(new NoteEvent { Time = 2.0, Lane = 1, Type = 0 });
            track.Notes.Add(new NoteEvent { Time = 3.0, Lane = 2, Type = 0 });
            track.Notes.Add(new NoteEvent { Time = 4.0, Lane = 0, Type = 0 });
            Project.NoteTracks.Add(track);
        }
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        Window.AllowUserResizing = true;
        _graphics.ApplyChanges();

        _layout = new PanelLayout();
        _transportBar = new TransportBarPanel();
        _timelinePanel = new TimelinePanel();
        _inspectorPanel = new InspectorPanel();
        _gameViewPanel = new GameViewPanel();

        _audio.PlaybackStopped += () => Transport.Pause();

        Window.FileDrop += OnFileDrop;
        Exiting += (_, _) => ProjectPersistence.Save(Project, Transport);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    private void OnFileDrop(object? sender, FileDropEventArgs e)
    {
        try
        {
            if (e.Files != null && e.Files.Length > 0)
                lock (_pendingDroppedFiles) { _pendingDroppedFiles.AddRange(e.Files); }
        }
        catch
        {
            // Ignore; we'll process on main thread
        }
    }

    /// <summary>Shows OpenFileDialog on an STA thread to avoid ThreadStateException / crash.</summary>
    private void OpenFileDialogOnStaThread()
    {
        string? path = null;
        var t = new Thread(() =>
        {
            try
            {
                path = AudioImportService.PickAudioFile();
            }
            catch
            {
                // Dialog failed; path stays null
            }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (path == null) return;
        // When user imports a new audio file, run BPM auto-detection.
        StartAudioLoad(path, detectBpm: true);
    }

    /// <summary>Shows SaveFileDialog for .agxbs on an STA thread, then saves project to the chosen path and remembers it.</summary>
    private void SaveProjectAsDialogOnStaThread()
    {
        string? path = _currentProjectPath;
        var t = new Thread(() =>
        {
            try
            {
                using var dlg = new SaveFileDialog
                {
                    Title = "Save project",
                    Filter = "AGX Beat Sync project (*.agxbs)|*.agxbs|All files (*.*)|*.*",
                    DefaultExt = "agxbs",
                    FilterIndex = 1
                };

                if (!string.IsNullOrWhiteSpace(path))
                {
                    try
                    {
                        dlg.InitialDirectory = System.IO.Path.GetDirectoryName(path);
                        dlg.FileName = System.IO.Path.GetFileName(path);
                    }
                    catch
                    {
                        // Ignore invalid path; fall back to defaults
                    }
                }

                if (dlg.ShowDialog() == DialogResult.OK)
                    path = dlg.FileName;
            }
            catch
            {
                // Dialog failed; path stays null
            }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (string.IsNullOrWhiteSpace(path)) return;
        _currentProjectPath = path;
        try
        {
            ProjectPersistence.SaveToFile(Project, Transport, path);
        }
        catch
        {
            // Could show a message; for now fail silently
        }
    }

    /// <summary>Saves the project to the current project path, or falls back to Save As if none.</summary>
    private void SaveProjectToCurrentPath()
    {
        if (string.IsNullOrWhiteSpace(_currentProjectPath))
        {
            SaveProjectAsDialogOnStaThread();
            return;
        }

        try
        {
            ProjectPersistence.SaveToFile(Project, Transport, _currentProjectPath);
        }
        catch
        {
            // Could show a message; for now fail silently
        }
    }

    private void StartAudioLoad(string path, bool detectBpm)
    {
        _audioLoad.Start(path, detectBpm);
    }

    private void ApplyLoadingResult(string path, WaveformCache cache, double? bpm)
    {
        Project.AudioFilePath = path;
        if (_audio.Load(path))
        {
            _waveformCache = cache;
            if (bpm.HasValue)
            {
                Transport.BPM = bpm.Value;
                Project.BPM = (float)bpm.Value;
            }
            _audio.Seek(Transport.CurrentTime);
            _playheadDisplayTime = Transport.CurrentTime;
            if (Transport.IsPlaying)
                _audio.Play();
        }
        _audioLoad.Reset();
    }

    private void ProcessDroppedFiles()
    {
        string[] toProcess;
        lock (_pendingDroppedFiles)
        {
            if (_pendingDroppedFiles.Count == 0) return;
            toProcess = _pendingDroppedFiles.ToArray();
            _pendingDroppedFiles.Clear();
        }
        foreach (string path in toProcess)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            if (ext is not ".mp3" and not ".wav") continue;
            // Drag-and-drop of a new audio file should also auto-detect BPM.
            StartAudioLoad(path, detectBpm: true);
            break;
        }
    }

    protected override void Update(GameTime gameTime)
    {
        if (_loadSavedAudioOnStart && !string.IsNullOrWhiteSpace(Project.AudioFilePath))
        {
            _loadSavedAudioOnStart = false;
            // On project/session load, preserve the saved BPM; do not auto-detect.
            StartAudioLoad(Project.AudioFilePath, detectBpm: false);
        }

        if (_audioLoad.TryComplete(out string path, out var cache, out double? bpm))
        {
            try { ApplyLoadingResult(path, cache, bpm); }
            catch { _audioLoad.Reset(); }
        }

        ProcessDroppedFiles();
        Input.Update();

        // Global shortcuts
        if (Input.IsKeyPressed(Keys.Space))
        {
            if (Transport.IsPlaying)
            {
                Transport.Pause();
                _audio.Pause();
            }
            else
            {
                Transport.Play();
                if (_audio.LoadedFilePath != null)
                {
                    _audio.Seek(Transport.CurrentTime);
                    _audio.Play();
                }
            }
        }
        if (Input.IsKeyPressed(Keys.Delete) && Selection.SelectedNoteTrack != null && Selection.SelectedNotes.Count > 0)
        {
            foreach (var note in Selection.SelectedNotes.ToList())
                CommandStack.Execute(new DeleteNoteCommand(Selection.SelectedNoteTrack, note));
            Selection.Clear();
        }
        if ((Input.IsKeyDown(Keys.LeftControl) || Input.IsKeyDown(Keys.RightControl)) && Input.IsKeyPressed(Keys.A))
        {
            if (Project.NoteTracks.Count > 0 && Project.NoteTracks[0].Notes.Count > 0)
                Selection.SetAllNotes(Project.NoteTracks[0]);
        }
        bool ctrl = Input.IsKeyDown(Keys.LeftControl) || Input.IsKeyDown(Keys.RightControl);
        if (ctrl && Input.IsKeyPressed(Keys.C) && Selection.SelectedNoteTrack != null && Selection.SelectedNotes.Count > 0)
        {
            double anchor = Selection.SelectedNotes.Min(n => n.Time);
            _noteClipboard.Clear();
            foreach (var n in Selection.SelectedNotes)
                _noteClipboard.Add((n.Time - anchor, n.Lane, n.Type));
        }
        if (ctrl && Input.IsKeyPressed(Keys.X) && Selection.SelectedNoteTrack != null && Selection.SelectedNotes.Count > 0)
        {
            double anchor = Selection.SelectedNotes.Min(n => n.Time);
            _noteClipboard.Clear();
            foreach (var n in Selection.SelectedNotes)
                _noteClipboard.Add((n.Time - anchor, n.Lane, n.Type));
            foreach (var note in Selection.SelectedNotes.ToList())
                CommandStack.Execute(new DeleteNoteCommand(Selection.SelectedNoteTrack, note));
            Selection.Clear();
        }
        if (ctrl && Input.IsKeyPressed(Keys.V) && _noteClipboard.Count > 0)
        {
            var track = Selection.SelectedNoteTrack ?? (Project.NoteTracks.Count > 0 ? Project.NoteTracks[0] : null);
            if (track != null)
            {
                double pasteTime = Transport.CurrentTime;
                var pasted = new List<NoteEvent>();
                foreach (var (timeOffset, lane, type) in _noteClipboard)
                {
                    var note = new NoteEvent { Time = pasteTime + timeOffset, Lane = lane, Type = type };
                    CommandStack.Execute(new AddNoteCommand(track, note));
                    pasted.Add(note);
                }
                Selection.SelectedNoteTrack = track;
                Selection.SelectedNotes = pasted;
                Selection.SelectedNote = pasted.Count > 0 ? pasted[0] : null;
            }
        }
        bool shift = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
        if ((Input.IsKeyDown(Keys.LeftControl) || Input.IsKeyDown(Keys.RightControl)) && Input.IsKeyPressed(Keys.Z) && !shift)
        {
            CommandStack.Undo();
            ClearSelectionIfNoteRemoved();
        }
        if ((Input.IsKeyDown(Keys.LeftControl) || Input.IsKeyDown(Keys.RightControl)) && (Input.IsKeyPressed(Keys.Y) || (shift && Input.IsKeyPressed(Keys.Z))))
        {
            CommandStack.Redo();
            ClearSelectionIfNoteRemoved();
        }
        if (ctrl && Input.IsKeyPressed(Keys.O))
        {
            OpenFileDialogOnStaThread();
        }
        if (ctrl && Input.IsKeyPressed(Keys.S))
        {
            if (shift)
                SaveProjectAsDialogOnStaThread();
            else
                SaveProjectToCurrentPath();
        }
        // BPM: [ and ] to decrease/increase (hold Shift for ±5)
        double bpmStep = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift) ? 5.0 : 1.0;
        if (Input.IsKeyPressed(Keys.OemCloseBrackets))
        {
            Transport.BPM = Math.Min(999, Transport.BPM + bpmStep);
            Project.BPM = (float)Transport.BPM;
        }
        if (Input.IsKeyPressed(Keys.OemOpenBrackets))
        {
            Transport.BPM = Math.Max(20, Transport.BPM - bpmStep);
            Project.BPM = (float)Transport.BPM;
        }
        // Grid size (Ableton-style): Ctrl+1 = finer, Ctrl+2 = coarser
        if (ctrl && Input.IsKeyPressed(Keys.D1))
        {
            TimelineView.GridSubdivisionsPerBeat = Math.Min(TimelineViewState.MaxGridSubdivisions, TimelineView.GridSubdivisionsPerBeat * 2);
        }
        if (ctrl && Input.IsKeyPressed(Keys.D2))
        {
            TimelineView.GridSubdivisionsPerBeat = Math.Max(TimelineViewState.MinGridSubdivisions, TimelineView.GridSubdivisionsPerBeat / 2);
        }

        // Drive transport from audio when playing with a loaded file, else game time
        if (Transport.IsPlaying)
        {
            if (_audio.LoadedFilePath != null && _audio.IsPlaying)
                Transport.CurrentTime = _audio.CurrentTimeSeconds;
            else if (_audio.LoadedFilePath == null)
                Transport.CurrentTime += gameTime.ElapsedGameTime.TotalSeconds;
            // Smooth playhead for display (NAudio reports position in steps; lerp removes jitter)
            const double playheadSmooth = 0.35;
            _playheadDisplayTime += (Transport.CurrentTime - _playheadDisplayTime) * playheadSmooth;
        }
        else
        {
            _playheadDisplayTime = Transport.CurrentTime;
        }

        // Sync back buffer to window size when user resizes
        var bounds = Window.ClientBounds;
        int w = Math.Max(320, bounds.Width);
        int h = Math.Max(240, bounds.Height);
        if (w != _graphics.PreferredBackBufferWidth || h != _graphics.PreferredBackBufferHeight)
        {
            _graphics.PreferredBackBufferWidth = w;
            _graphics.PreferredBackBufferHeight = h;
            _graphics.ApplyChanges();
        }

        if (_gameViewResizeDragging)
        {
            int minH = PanelLayout.MinGameViewHeight;
            int maxH = Math.Max(minH, _graphics.PreferredBackBufferHeight - PanelLayout.TransportHeight - PanelLayout.MinTimelineHeight);
            int newH = _gameViewResizeStartHeight + (Input.MousePosition.Y - _gameViewResizeStartY);
            _layout.GameViewHeightPx = Math.Clamp(newH, minH, maxH);
            if (Input.MouseLeftReleased)
                _gameViewResizeDragging = false;
        }

        _layout.Update(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);

        if (!_gameViewResizeDragging && Input.MouseLeftPressed && _layout.DividerGrip.Contains(Input.MousePosition))
        {
            _gameViewResizeDragging = true;
            _gameViewResizeStartY = Input.MousePosition.Y;
            _gameViewResizeStartHeight = _layout.GameViewHeightPx;
        }
        _transportBar.Bounds = _layout.TransportBar;
        _transportBar.Project = Project;
        _transportBar.Transport = Transport;
        _transportBar.Input = Input;
        _transportBar.BpmEditRequested = () =>
        {
            string? result = null;
            var t = new Thread(() => result = BpmInputDialog.Show(Transport.BPM))
            {
                IsBackground = true
            };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (result != null && double.TryParse(result.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double bpm) && bpm >= 20 && bpm <= 999)
            {
                Transport.BPM = bpm;
                Project.BPM = (float)bpm;
            }
        };
        _transportBar.TimeEditRequested = () =>
        {
            double? result = null;
            var thread = new Thread(() => result = TimeInputDialog.Show(_playheadDisplayTime))
            {
                IsBackground = true
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (result.HasValue && result.Value >= 0)
            {
                Transport.Seek(result.Value);
                _playheadDisplayTime = result.Value;
                if (_audio.LoadedFilePath != null)
                    _audio.Seek(result.Value);
            }
        };
        _timelinePanel.Bounds = _layout.Timeline;

        Window.Title = $"AGX Beat Sync — BPM: {Transport.BPM:F0} | {TimeFormatHelper.Format(_playheadDisplayTime)}";
        _inspectorPanel.Bounds = _layout.Inspector;
        _gameViewPanel.Bounds = _layout.GameView;
        _gameViewPanel.Input = Input;
        _gameViewPanel.Project = Project;
        _gameViewPanel.Transport = Transport;

        Transport.BeatOffsetSeconds = Project.BeatOffsetSeconds;
        _timelinePanel.Project = Project;
        _timelinePanel.Transport = Transport;
        _timelinePanel.PlayheadDisplayTime = _playheadDisplayTime;
        _timelinePanel.ViewState = TimelineView;
        _timelinePanel.Input = Input;
        _timelinePanel.Selection = Selection;
        _timelinePanel.Waveform = _waveformCache;
        _timelinePanel.CommandStack = CommandStack;
        _timelinePanel.SeekRequested = (t) =>
        {
            Transport.Seek(t);
            _playheadDisplayTime = t;
            if (_audio.LoadedFilePath != null)
                _audio.Seek(t);
        };
        _timelinePanel.OffsetChanged = (offset) =>
        {
            Project.BeatOffsetSeconds = offset;
            Transport.BeatOffsetSeconds = offset;
        };
        _timelinePanel.OffsetEditRequested = () =>
        {
            double? result = null;
            var thread = new Thread(() => result = TimeInputDialog.Show(Project.BeatOffsetSeconds))
            {
                IsBackground = true
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (result.HasValue && result.Value >= 0)
            {
                Project.BeatOffsetSeconds = result.Value;
                Transport.BeatOffsetSeconds = result.Value;
            }
        };
        _inspectorPanel.Selection = Selection;

        _transportBar.Update(gameTime);
        _audio.Volume = _transportBar.Volume;
        _timelinePanel.Update(gameTime);
        _inspectorPanel.Update(gameTime);
        _gameViewPanel.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(28, 30, 34));

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        _transportBar.Draw(_spriteBatch);
        _timelinePanel.Draw(_spriteBatch);
        _inspectorPanel.Draw(_spriteBatch);

        if (_audioLoad.IsLoading)
        {
            var pixel = PanelBase.GetPixelTexture(GraphicsDevice);
            LoadingOverlay.Draw(_spriteBatch, pixel, GraphicsDevice, _audioLoad.Progress, gameTime);
        }

        _gameViewPanel.Draw(_spriteBatch);

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        try { Window.FileDrop -= OnFileDrop; } catch { }
        _audio.Dispose();
        base.UnloadContent();
    }
}
