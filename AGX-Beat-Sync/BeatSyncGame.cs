using System.Diagnostics;
using System.Runtime.InteropServices;
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
    private EventTrackListPanel _trackListPanel = null!;
    private TimelinePanel _timelinePanel = null!;
    private InspectorPanel _inspectorPanel = null!;
    private GameViewPanel _gameViewPanel = null!;
    private StatusBarPanel _statusBarPanel = null!;
    private OpenDialogPanel _openDialogPanel = null!;

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
    private readonly AudioLoadCoordinator _audioLoad = new();
    /// <summary>Fired (trackIndex, eventTime) so we don't double-fire when playback crosses an event time.</summary>
    private readonly HashSet<(int trackIndex, double eventTime)> _eventFiredSet = new();
    private double _lastPlaybackTime = -1;

    private bool _gameViewResizeDragging;
    private int _gameViewResizeStartY;
    private int _gameViewResizeStartHeight;

    // CPU / Memory metrics (top right of whole view)
    private const int MetricsMargin = 8;
    private const int MetricsFontSize = 18;       // render at 18; scale to fit max size (aspect preserved)
    private const int MetricsMaxWidth = 260;
    private const int MetricsMaxHeight = 40;
    private const float MetricsSampleInterval = 0.25f;
    private const float MetricsAlphaLow = 0.06f;   // more transparent when CPU < 80%
    private const float MetricsAlphaHigh = 0.5f;   // more transparent when CPU >= 80%
    private const float MetricsCpuThreshold = 80f;
    private const long MetricsMemoryThresholdMb = 800;
    private readonly Process _metricsProcess = Process.GetCurrentProcess();
    private TimeSpan _metricsLastCpuTime;
    private double _metricsLastCpuSampleTime = -1;
    private float _metricsCpuPercent;
    private long _metricsMemoryMb;
    private float _metricsSampleAccum;
    private string _metricsLastString = "";
    private Texture2D? _metricsTexture;
    private Texture2D? _playerTexture;

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

    private void EnsureDefaultTracks()
    {
        if (Project.EventTracks.Count == 0)
        {
            var track = new SpawnEntityTrack { Order = 0 };
            track.EventTimes.Add(1.0);
            track.EventTimes.Add(2.0);
            track.EventTimes.Add(3.0);
            track.EventTimes.Add(4.0);
            Project.EventTracks.Add(track);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>Bring game window to foreground so keyboard shortcuts work (e.g. after a WinForms dialog closed).</summary>
    private void RestoreWindowFocus()
    {
        try { SetForegroundWindow(Window.Handle); } catch { /* ignore */ }
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        Window.AllowUserResizing = true;
        _graphics.ApplyChanges();

        _layout = new PanelLayout();
        _transportBar = new TransportBarPanel();
        _trackListPanel = new EventTrackListPanel();
        _timelinePanel = new TimelinePanel();
        _inspectorPanel = new InspectorPanel();
        _gameViewPanel = new GameViewPanel();
        _statusBarPanel = new StatusBarPanel();
        _openDialogPanel = new OpenDialogPanel();

        // Register event track types and their inspector renderers
        EventTrackRegistry.Register(new EventTrackDescriptor("SpawnEntity", "Spawn Entity", () => new SpawnEntityTrack()));
        InspectorRendererRegistry.Register("SpawnEntity", new SpawnEntityInspectorRenderer());

        _audio.PlaybackStopped += () => Transport.Pause();

        Window.FileDrop += OnFileDrop;
        Window.KeyDown += (_, e) => Input.OnKeyDown(e.Key);
        Window.KeyUp += (_, e) => Input.OnKeyUp(e.Key);
        Deactivated += (_, _) => Input.ClearKeys();
        Exiting += (_, _) => ProjectPersistence.Save(Project, Transport);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _gameViewPanel.GraphicsDevice = GraphicsDevice;
        _playerTexture = LoadPlayerTexture();
        _gameViewPanel.PlayerTexture = _playerTexture;
    }

    /// <summary>Load player sprite from Content Pipeline (.xnb) or fallback to Content/player.png on disk.</summary>
    private Texture2D? LoadPlayerTexture()
    {
        try
        {
            return Content.Load<Texture2D>("player");
        }
        catch
        {
            // Fallback: load from file if MGCB wasn't run or content missing
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Content", "player.png");
                if (File.Exists(path))
                {
                    using var stream = File.OpenRead(path);
                    return Texture2D.FromStream(GraphicsDevice, stream);
                }
            }
            catch { /* ignore */ }
            return null;
        }
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

    /// <summary>Handles result after the in-game Open dialog is closed.</summary>
    private void ApplyOpenDialogResult()
    {
        if (_openDialogPanel.SelectedProjectPath != null)
        {
            string path = _openDialogPanel.SelectedProjectPath;
            _openDialogPanel.ClearResult();
            if (ProjectPersistence.TryLoadFromFile(path, out var saved) && saved != null)
            {
                ProjectPersistence.ApplyState(saved, Project, Transport);
                EnsureDefaultTracks();
                _currentProjectPath = path;
                ProjectPersistence.AddRecentProjectPath(path);
                if (!string.IsNullOrWhiteSpace(Project.AudioFilePath))
                    _loadSavedAudioOnStart = true;
            }
            return;
        }
        if (_openDialogPanel.BrowseMusicRequested)
        {
            _openDialogPanel.ClearResult();
            string? path = null;
            var t = new Thread(() => { try { path = AudioImportService.PickAudioFile(); } catch { } });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            RestoreWindowFocus();
            if (path != null)
                StartAudioLoad(path, detectBpm: true);
            return;
        }
        if (_openDialogPanel.BrowseProjectRequested)
        {
            _openDialogPanel.ClearResult();
            string? path = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var dlg = new OpenFileDialog
                    {
                        Title = "Open project",
                        Filter = "AGX Beat Sync project (*.agxbs)|*.agxbs|All files (*.*)|*.*",
                        FilterIndex = 1
                    };
                    if (dlg.ShowDialog() == DialogResult.OK)
                        path = dlg.FileName;
                }
                catch { }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            RestoreWindowFocus();
            if (path != null && ProjectPersistence.TryLoadFromFile(path, out var saved) && saved != null)
            {
                ProjectPersistence.ApplyState(saved, Project, Transport);
                EnsureDefaultTracks();
                _currentProjectPath = path;
                ProjectPersistence.AddRecentProjectPath(path);
                if (!string.IsNullOrWhiteSpace(Project.AudioFilePath))
                    _loadSavedAudioOnStart = true;
            }
            return;
        }
        _openDialogPanel.ClearResult();
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
        RestoreWindowFocus();
        if (string.IsNullOrWhiteSpace(path)) return;
        _currentProjectPath = path;
        try
        {
            ProjectPersistence.SaveToFile(Project, Transport, path);
            ProjectPersistence.AddRecentProjectPath(path);
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
        Input.Update(IsActive);

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
        if (Input.IsKeyPressed(Keys.Delete))
        {
            if (Selection.SelectedEventTrack is EventTrackBase eb && Project.EventTracks.Contains(eb))
            {
                if (Selection.SelectedEventTime.HasValue)
                {
                    double t = Selection.SelectedEventTime.Value;
                    eb.EventTimes.Remove(t);
                    eb.EventDurations.Remove(t);
                    Selection.SelectedEventTime = null;
                }
                else
                {
                    Project.EventTracks.Remove(eb);
                    Selection.SelectedEventTrack = Project.EventTracks.FirstOrDefault();
                }
            }
        }
        bool shift = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
        bool ctrl = Input.IsKeyDown(Keys.LeftControl) || Input.IsKeyDown(Keys.RightControl);
        if (ctrl && Input.IsKeyPressed(Keys.Z) && !shift)
            CommandStack.Undo();
        if (ctrl && (Input.IsKeyPressed(Keys.Y) || (shift && Input.IsKeyPressed(Keys.Z))))
            CommandStack.Redo();
        if (ctrl && Input.IsKeyPressed(Keys.O))
        {
            _openDialogPanel.Open();
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

        // Seek backward: clear fired events and spawned entities so replay is correct
        if (Transport.CurrentTime < _lastPlaybackTime)
        {
            _eventFiredSet.Clear();
            _gameViewPanel.ClearSpawnedEntities();
        }

        // Drive transport from audio when playing with a loaded file, else game time
        double prevTime = Transport.CurrentTime;
        if (Transport.IsPlaying)
        {
            if (_audio.LoadedFilePath != null && _audio.IsPlaying)
                Transport.CurrentTime = _audio.CurrentTimeSeconds;
            else if (_audio.LoadedFilePath == null)
                Transport.CurrentTime += gameTime.ElapsedGameTime.TotalSeconds;
            const double playheadSmooth = 0.35;
            _playheadDisplayTime += (Transport.CurrentTime - _playheadDisplayTime) * playheadSmooth;
        }
        else
            _playheadDisplayTime = Transport.CurrentTime;

        // Fire event tracks when playback crosses an event time
        if (Transport.IsPlaying && Transport.CurrentTime >= 0)
        {
            for (int ti = 0; ti < Project.EventTracks.Count; ti++)
            {
                var track = Project.EventTracks[ti];
                foreach (var eventTime in track.EventTimes)
                {
                    if (eventTime > prevTime && eventTime <= Transport.CurrentTime && _eventFiredSet.Add((ti, eventTime)))
                    {
                        if (track is SpawnEntityTrack spawnTrack)
                        {
                            var pos = spawnTrack.PositionMode == PositionMode.Origin ? new Vector3(0, 1, 0)
                                : spawnTrack.PositionMode == PositionMode.Absolute ? spawnTrack.PositionAbsolute
                                : spawnTrack.PositionRelative;
                            var rot = spawnTrack.RotationMode == RotationMode.Absolute ? spawnTrack.RotationEuler : Vector3.Zero;
                            _gameViewPanel.SpawnEntity(pos, rot, spawnTrack.Speed);
                        }
                    }
                }
            }
        }
        _lastPlaybackTime = Transport.CurrentTime;

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
            int maxH = Math.Max(minH, _graphics.PreferredBackBufferHeight - PanelLayout.TransportHeight - PanelLayout.MinTimelineHeight - PanelLayout.StatusBarHeight);
            int newH = _gameViewResizeStartHeight + (_gameViewResizeStartY - Input.MousePosition.Y);
            _layout.GameViewHeightPx = Math.Clamp(newH, minH, maxH);
            if (Input.MouseLeftReleased)
                _gameViewResizeDragging = false;
        }

        _layout.Update(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);

        _openDialogPanel.GraphicsDevice = GraphicsDevice;
        _openDialogPanel.Input = Input;
        if (_openDialogPanel.IsVisible)
        {
            _openDialogPanel.Update(gameTime, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
            if (!_openDialogPanel.IsVisible)
                ApplyOpenDialogResult();
            Window.Title = $"AGX Beat Sync — BPM: {Transport.BPM:F0} | {TimeFormatHelper.Format(_playheadDisplayTime)}";
            UpdateMetrics(gameTime);
            base.Update(gameTime);
            return;
        }

        if (!_gameViewResizeDragging && Input.MouseLeftPressed && _layout.DividerGrip.Contains(Input.MousePosition))
        {
            _gameViewResizeDragging = true;
            _gameViewResizeStartY = Input.MousePosition.Y;
            _gameViewResizeStartHeight = _layout.GameViewHeightPx;
        }
        _transportBar.Bounds = _layout.TransportBar;
        _trackListPanel.Bounds = _layout.TrackList;
        _trackListPanel.Project = Project;
        _trackListPanel.Selection = Selection;
        _trackListPanel.Input = Input;
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
            RestoreWindowFocus();
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
            RestoreWindowFocus();
            if (result.HasValue && result.Value >= 0)
            {
                Transport.Seek(result.Value);
                _playheadDisplayTime = result.Value;
                if (_audio.LoadedFilePath != null)
                    _audio.Seek(result.Value);
            }
        };
        _transportBar.OffsetChanged = (offset) =>
        {
            Project.BeatOffsetSeconds = offset;
            Transport.BeatOffsetSeconds = offset;
        };
        _transportBar.OffsetEditRequested = () =>
        {
            double? result = null;
            var thread = new Thread(() => result = TimeInputDialog.Show(Project.BeatOffsetSeconds, TimeFormatHelper.DefaultFramesPerSecond, "Enter offset"))
            {
                IsBackground = true
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            RestoreWindowFocus();
            if (result.HasValue && result.Value >= 0)
            {
                Project.BeatOffsetSeconds = result.Value;
                Transport.BeatOffsetSeconds = result.Value;
            }
        };
        _timelinePanel.Bounds = _layout.Timeline;
        _timelinePanel.IgnoreClickRect = _layout.DividerGrip;

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
        _inspectorPanel.Selection = Selection;
        _inspectorPanel.Input = Input;

        _statusBarPanel.Bounds = _layout.StatusBar;
        string? hoverText = null;
        MouseCursor? desiredCursor = null;
        if (_layout.DividerGrip.Contains(Input.MousePosition))
            hoverText = "Drag to resize timeline / game view";
        else if (_transportBar.ContainsPoint(Input.MousePosition))
            hoverText = _transportBar.GetHoverText(Input.MousePosition);
        else if (_trackListPanel.ContainsPoint(Input.MousePosition))
            hoverText = _trackListPanel.GetHoverText(Input.MousePosition);
        else if (_timelinePanel.ContainsPoint(Input.MousePosition))
        {
            hoverText = _timelinePanel.GetHoverText(Input.MousePosition);
            desiredCursor = _timelinePanel.GetDesiredCursor(Input.MousePosition);
        }
        else if (_inspectorPanel.ContainsPoint(Input.MousePosition))
            hoverText = _inspectorPanel.GetHoverText(Input.MousePosition);
        else if (_gameViewPanel.ContainsPoint(Input.MousePosition))
            hoverText = _gameViewPanel.GetHoverText(Input.MousePosition);
        _statusBarPanel.HoverText = hoverText ?? "";
        try { Mouse.SetCursor(desiredCursor ?? MouseCursor.Arrow); } catch { /* ignore on some platforms */ }

        _transportBar.Update(gameTime);
        _audio.Volume = _transportBar.Volume;
        _trackListPanel.Update(gameTime);
        _timelinePanel.Update(gameTime);
        _inspectorPanel.Update(gameTime);
        _gameViewPanel.Update(gameTime);
        IsMouseVisible = !_gameViewPanel.IsCapturingMouse;

        UpdateMetrics(gameTime);
        base.Update(gameTime);
    }

    private void UpdateMetrics(GameTime gameTime)
    {
        _metricsSampleAccum += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_metricsSampleAccum < MetricsSampleInterval)
            return;
        _metricsSampleAccum = 0;

        try { _metricsProcess.Refresh(); } catch { /* ignore */ }
        double totalSeconds = gameTime.TotalGameTime.TotalSeconds;
        TimeSpan cpuTime = _metricsProcess.TotalProcessorTime;

        if (_metricsLastCpuSampleTime >= 0)
        {
            double wallDelta = totalSeconds - _metricsLastCpuSampleTime;
            if (wallDelta > 0.01)
            {
                double cpuDelta = (cpuTime - _metricsLastCpuTime).TotalSeconds;
                _metricsCpuPercent = (float)((cpuDelta / wallDelta) * 100);
                if (_metricsCpuPercent > 99.9f) _metricsCpuPercent = 99.9f;
            }
        }
        _metricsLastCpuTime = cpuTime;
        _metricsLastCpuSampleTime = totalSeconds;
        _metricsMemoryMb = _metricsProcess.WorkingSet64 / (1024 * 1024);
    }

    protected override void Draw(GameTime gameTime)
    {
        var gd = GraphicsDevice;
        // Ensure full back buffer viewport so mid-frame RT switches (inspector, game view) don't leave it small next frame
        gd.Viewport = new Viewport(0, 0, gd.PresentationParameters.BackBufferWidth, gd.PresentationParameters.BackBufferHeight);
        gd.ScissorRectangle = gd.Viewport.Bounds;
        gd.Clear(new Color(28, 30, 34));

        // CullNone so that when InspectorPanel flushes the batch mid-frame (End/Begin for scissor), the flush doesn't cull 2D sprites and turn the timeline black
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, RasterizerState.CullNone);

        // Draw inspector first so its mid-frame End/RT/Begin only flushes inspector bg+header; transport, track list, timeline go in the batch after restore so they stay visible
        _inspectorPanel.Draw(_spriteBatch);
        _transportBar.Draw(_spriteBatch);
        _trackListPanel.Draw(_spriteBatch);
        _timelinePanel.Draw(_spriteBatch);

        if (_audioLoad.IsLoading)
        {
            var pixel = PanelBase.GetPixelTexture(GraphicsDevice);
            LoadingOverlay.Draw(_spriteBatch, pixel, GraphicsDevice, _audioLoad.Progress, gameTime);
        }

        _gameViewPanel.Draw3DScene();
        _gameViewPanel.Draw(_spriteBatch);

        var grip = _layout.DividerGrip;
        var gripPixel = PanelBase.GetPixelTexture(GraphicsDevice);
        _spriteBatch.Draw(gripPixel, grip, new Color(70, 75, 85));
        _spriteBatch.Draw(gripPixel, new Rectangle(grip.X, grip.Y + grip.Height / 2 - 1, grip.Width, 2), new Color(110, 118, 132));

        DrawMetricsOverlay(gameTime);
        _statusBarPanel.Draw(_spriteBatch);

        if (_openDialogPanel.IsVisible)
            _openDialogPanel.Draw(_spriteBatch);

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void DrawMetricsOverlay(GameTime gameTime)
    {
        int viewW = GraphicsDevice.Viewport.Width;
        string s = $"CPU: {_metricsCpuPercent:F1}%  Mem: {_metricsMemoryMb} MB";
        if (s != _metricsLastString)
        {
            _metricsTexture?.Dispose();
            _metricsTexture = TextTextureHelper.Create(GraphicsDevice, s, "Segoe UI", MetricsFontSize);
            _metricsLastString = s;
        }
        if (_metricsTexture == null)
            return;
        int tw = _metricsTexture.Width;
        int th = _metricsTexture.Height;
        float scale = Math.Min((float)MetricsMaxWidth / tw, (float)MetricsMaxHeight / th);
        scale = Math.Min(scale, 1f); // never scale up
        int w = (int)(tw * scale);
        int h = (int)(th * scale);
        int x = viewW - MetricsMargin - w;
        int y = MetricsMargin;
        var dest = new Rectangle(x, y, w, h);
        var src = new Rectangle(0, 0, tw, th);
        bool problematic = _metricsCpuPercent >= MetricsCpuThreshold || _metricsMemoryMb >= MetricsMemoryThresholdMb;
        byte alpha = (byte)((problematic ? MetricsAlphaHigh : MetricsAlphaLow) * 255);
        byte gray = (byte)(problematic ? 255 : 130);   // grayed out when not problematic
        var tint = new Color(gray, gray, gray, alpha);
        var bgAlpha = (byte)((problematic ? 0.35f : 0.04f) * 255);
        var bg = new Rectangle(x - 3, y - 1, w + 6, h + 2);
        var pixel = PanelBase.GetPixelTexture(GraphicsDevice);
        _spriteBatch.Draw(pixel, bg, new Color((byte)0, (byte)0, (byte)0, bgAlpha));
        _spriteBatch.Draw(_metricsTexture, dest, src, tint);
    }

    protected override void UnloadContent()
    {
        try { Window.FileDrop -= OnFileDrop; } catch { }
        _audio.Dispose();
        base.UnloadContent();
    }
}
