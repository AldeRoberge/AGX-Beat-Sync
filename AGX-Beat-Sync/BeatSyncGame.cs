using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using AGX_Beat_Sync.Audio;
using AGX_Beat_Sync.Commands;
using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Editor;
using AGX_Beat_Sync.Input;
using AGX_Beat_Sync.Native;
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
    private EventConsolePanel _eventConsolePanel = null!;
    private GameViewPanel _gameViewPanel = null!;
    private StatusBarPanel _statusBarPanel = null!;
    private OpenDialogPanel _openDialogPanel = null!;
    private OptionsDialogPanel _optionsDialogPanel = null!;
    private HeaderBarPanel _headerBarPanel = null!;
    private EventTrackBase? _pendingDeleteTrack;
    private List<EventTrackBase>? _pendingDeleteTracks;

    public Project Project { get; }
    public Transport Transport { get; }
    /// <summary>When true, keys 1-9 and 0 add events to tracks 1-10 while playing. Toggle with R.</summary>
    public bool RecordMode => _recordMode;
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
    /// <summary>Game view height from saved session; applied in Initialize after layout is created.</summary>
    private int? _savedGameViewHeightPx;
    /// <summary>Game view width (bottom row) from saved session; applied in Initialize.</summary>
    private int? _savedGameViewWidthPx;
    private int? _savedInspectorWidthPx;
    /// <summary>Camera state from saved session; applied in Initialize when CameraOrbitDistance >= 1.</summary>
    private float? _savedCameraTargetX;
    private float? _savedCameraTargetY;
    private float? _savedCameraTargetZ;
    private float? _savedCameraOrbitYaw;
    private float? _savedCameraOrbitPitch;
    private float? _savedCameraOrbitDistance;
    private readonly AudioLoadCoordinator _audioLoad = new();
    /// <summary>Fired (trackIndex, eventTime) so we don't double-fire when playback crosses an event time.</summary>
    private readonly HashSet<(int trackIndex, double eventTime)> _eventFiredSet = new();
    private double _lastPlaybackTime = -1;

    /// <summary>When true, pressing 1-9/0 while playing adds an event at current time to the corresponding track (1=first track, 0=10th). Toggle with R.</summary>
    private bool _recordMode;

    /// <summary>When set, next Update shows "Saved to ..." in the status bar for a few seconds.</summary>
    private string? _pendingSavedPath;
    /// <summary>Current "Saved to ..." message and when it expires (TotalGameTime.TotalSeconds).</summary>
    private string? _statusBarSavedMessage;
    private double? _statusBarSavedMessageUntil;

    private bool _gameViewResizeDragging;
    private int _gameViewResizeStartY;
    private int _gameViewResizeStartHeight;
    private bool _bottomRowResizeDragging;
    private int _bottomRowResizeStartX;
    private int _bottomRowResizeStartWidth;
    private bool _inspectorResizeDragging;
    private int _inspectorResizeStartX;
    private int _inspectorResizeStartWidth;

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
    private float _metricsFps;
    private float _metricsSampleAccum;
    private int _metricsFrameCount;
    private string _metricsLastString = "";
    private Texture2D? _metricsTexture;
    private Texture2D? _playerTexture;

    /// <summary>If set before Run(), the game loads this .agxbs project at startup (e.g. from command line or file association).</summary>
    public static string? StartupProjectPath { get; set; }

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

        string? fileToOpen = StartupProjectPath;
        if (fileToOpen != null)
            StartupProjectPath = null; // clear so next run doesn't reuse it

        if (fileToOpen != null && ProjectPersistence.TryLoadFromFile(fileToOpen, out var savedFile) && savedFile != null)
        {
            ProjectPersistence.ApplyState(savedFile, Project, Transport, TimelineView);
            _playheadDisplayTime = Transport.CurrentTime;
            _currentProjectPath = fileToOpen;
            ResolveProjectAudioPath();
            ProjectPersistence.AddRecentProjectPath(fileToOpen);
            _savedGameViewHeightPx = savedFile.GameViewHeightPx;
            _savedGameViewWidthPx = savedFile.GameViewWidthPx != 0 ? savedFile.GameViewWidthPx : null;
            _savedInspectorWidthPx = savedFile.InspectorWidthPx != 0 ? savedFile.InspectorWidthPx : null;
            if (savedFile.CameraOrbitDistance >= 1f)
            {
                _savedCameraTargetX = savedFile.CameraTargetX;
                _savedCameraTargetY = savedFile.CameraTargetY;
                _savedCameraTargetZ = savedFile.CameraTargetZ;
                _savedCameraOrbitYaw = savedFile.CameraOrbitYaw;
                _savedCameraOrbitPitch = savedFile.CameraOrbitPitch;
                _savedCameraOrbitDistance = savedFile.CameraOrbitDistance;
            }
            EnsureDefaultTracks();
            if (!string.IsNullOrWhiteSpace(Project.AudioFilePath))
                _loadSavedAudioOnStart = true;
        }
        else if (ProjectPersistence.TryLoad(out var saved) && saved != null)
        {
            ProjectPersistence.ApplyState(saved, Project, Transport, TimelineView);
            _playheadDisplayTime = Transport.CurrentTime;
            _savedGameViewHeightPx = saved.GameViewHeightPx;
            _savedGameViewWidthPx = saved.GameViewWidthPx != 0 ? saved.GameViewWidthPx : null;
            _savedInspectorWidthPx = saved.InspectorWidthPx != 0 ? saved.InspectorWidthPx : null;
            if (saved.CameraOrbitDistance >= 1f)
            {
                _savedCameraTargetX = saved.CameraTargetX;
                _savedCameraTargetY = saved.CameraTargetY;
                _savedCameraTargetZ = saved.CameraTargetZ;
                _savedCameraOrbitYaw = saved.CameraOrbitYaw;
                _savedCameraOrbitPitch = saved.CameraOrbitPitch;
                _savedCameraOrbitDistance = saved.CameraOrbitDistance;
            }
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
            var track = new EmptyTrack { Order = 0, TrackColor = EventTrackBase.GetRandomTrackColor() };
            Project.EventTracks.Add(track);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    private const uint GA_ROOT = 2;

    /// <summary>Cached Win32 HWND from SDL (DesktopGL gives SDL pointer in Window.Handle, not HWND). Used for SetForegroundWindow and title bar.</summary>
    private IntPtr _cachedHwnd = IntPtr.Zero;
    private bool _cachedHwndResolved;

    /// <summary>Returns the Win32 HWND for our game window. On DesktopGL this comes from SDL_GetWindowWMInfo; otherwise Window.Handle. Returns IntPtr.Zero when on Windows but SDL did not provide an HWND (so foreground/shortcuts fall back to IsActive).</summary>
    private IntPtr GetWindowHwnd()
    {
        if (_cachedHwndResolved) return _cachedHwnd;
        _cachedHwndResolved = true;
        if (OperatingSystem.IsWindows() && Native.SdlWin32.TryGetHwndFromSdlWindow(Window.Handle, out IntPtr hwnd))
            _cachedHwnd = hwnd;
        else if (!OperatingSystem.IsWindows())
            _cachedHwnd = Window.Handle;
        // When on Windows and SDL didn't give us an HWND, leave _cachedHwnd zero so we don't compare HWND to SDL pointer (IsGameWindowForeground would never be true and shortcuts would rely on IsActive only).
        return _cachedHwnd;
    }

    /// <summary>True when our game window (or a child) is the foreground window. Uses SDL HWND when available, else Process.MainWindowHandle so focus/shortcuts work when SDL_GetWindowWMInfo fails.</summary>
    private bool IsGameWindowForeground()
    {
        if (!OperatingSystem.IsWindows()) return false;
        IntPtr ourHwnd = GetWindowHwnd();
        if (ourHwnd == IntPtr.Zero)
        {
            try { ourHwnd = Process.GetCurrentProcess().MainWindowHandle; } catch { }
            if (ourHwnd == IntPtr.Zero) return false;
        }
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        IntPtr root = GetAncestor(fg, GA_ROOT);
        return root == ourHwnd;
    }

    /// <summary>Bring game window to foreground (e.g. after a WinForms dialog closed). Uses GetWindowHwnd() or Process.MainWindowHandle.</summary>
    private void RestoreWindowFocus()
    {
        IntPtr hwnd = GetWindowHwnd();
        if (hwnd == IntPtr.Zero && OperatingSystem.IsWindows())
        {
            try { hwnd = Process.GetCurrentProcess().MainWindowHandle; } catch { }
        }
        if (hwnd == IntPtr.Zero) return;
        try { SetForegroundWindow(hwnd); } catch { /* ignore */ }
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        Window.AllowUserResizing = true;
        _graphics.ApplyChanges();

        TitleBarDarkMode.Apply(GetWindowHwnd());

        _layout = new PanelLayout();
        int w = _graphics.PreferredBackBufferWidth;
        int h = _graphics.PreferredBackBufferHeight;
        if (_savedGameViewHeightPx.HasValue)
        {
            int maxH = Math.Max(PanelLayout.MinGameViewHeight, h - PanelLayout.HeaderBarHeight - PanelLayout.TransportHeight - PanelLayout.TimelineStripsHeight - PanelLayout.MinTimelineHeight - PanelLayout.StatusBarHeight);
            _layout.GameViewHeightPx = Math.Clamp(_savedGameViewHeightPx.Value, PanelLayout.MinGameViewHeight, maxH);
            _savedGameViewHeightPx = null;
        }
        if (_savedGameViewWidthPx.HasValue)
        {
            int maxW = Math.Max(PanelLayout.MinGameViewWidth, w - PanelLayout.MinEventConsoleWidth);
            _layout.GameViewWidthPx = Math.Clamp(_savedGameViewWidthPx.Value, PanelLayout.MinGameViewWidth, maxW);
            _savedGameViewWidthPx = null;
        }
        if (_savedInspectorWidthPx.HasValue)
        {
            int maxW = Math.Max(PanelLayout.MinInspectorWidth, w - PanelLayout.TrackListWidth - PanelLayout.MinCenterWidth);
            _layout.InspectorWidthPx = Math.Clamp(_savedInspectorWidthPx.Value, PanelLayout.MinInspectorWidth, maxW);
            _savedInspectorWidthPx = null;
        }
        _transportBar = new TransportBarPanel();
        _trackListPanel = new EventTrackListPanel();
        _timelinePanel = new TimelinePanel();
        _inspectorPanel = new InspectorPanel();
        _eventConsolePanel = new EventConsolePanel();
        _gameViewPanel = new GameViewPanel();
        if (_savedCameraTargetX.HasValue && _savedCameraOrbitDistance.HasValue)
        {
            _gameViewPanel.SetCameraState(
                new Microsoft.Xna.Framework.Vector3(_savedCameraTargetX.Value, _savedCameraTargetY ?? 0.5f, _savedCameraTargetZ ?? 0f),
                _savedCameraOrbitYaw ?? -0.26f, _savedCameraOrbitPitch ?? -0.644f, _savedCameraOrbitDistance.Value);
            _savedCameraTargetX = _savedCameraTargetY = _savedCameraTargetZ = null;
            _savedCameraOrbitYaw = _savedCameraOrbitPitch = _savedCameraOrbitDistance = null;
        }
        _statusBarPanel = new StatusBarPanel();
        _openDialogPanel = new OpenDialogPanel();
        _optionsDialogPanel = new OptionsDialogPanel();
        _headerBarPanel = new HeaderBarPanel();

        // Register event track types and their inspector renderers (Empty first = default for new tracks)
        EventTrackRegistry.Register(new EventTrackDescriptor("Empty", "Empty", () => new EmptyTrack()));
        InspectorRendererRegistry.Register("Empty", new EmptyInspectorRenderer());
        EventTrackRegistry.Register(new EventTrackDescriptor("SpawnEntity", "Spawn Entity", () => new SpawnEntityTrack()));
        InspectorRendererRegistry.Register("SpawnEntity", new SpawnEntityInspectorRenderer());
        EventTrackRegistry.Register(new EventTrackDescriptor("SFX", "SFX", () => new SfxTrack()));
        InspectorRendererRegistry.Register("SFX", new SfxInspectorRenderer());
        EventTrackRegistry.Register(new EventTrackDescriptor("ChangeEntityColor", "Change Entity Color", () => new ChangeEntityColorTrack()));
        InspectorRendererRegistry.Register("ChangeEntityColor", new ChangeEntityColorInspectorRenderer());

        _audio.PlaybackStopped += () => Transport.Pause();

        Window.FileDrop += OnFileDrop;
        Window.KeyDown += OnKeyDown;
        Window.KeyUp += (_, e) => Input.OnKeyUp(e.Key);
        Deactivated += (_, _) => Input.ClearKeys();
        Exiting += (_, _) =>
        {
            var (target, oyaw, opitch, odist) = _gameViewPanel.GetCameraState();
            ProjectPersistence.Save(Project, Transport, TimelineView, _layout.GameViewHeightPx, _layout.GameViewWidthPx, _layout.InspectorWidthPx,
                target.X, target.Y, target.Z, oyaw, opitch, odist, _currentProjectPath);
        };

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

    /// <summary>Track key in KeyDown so merged keyboard state is correct. Edit shortcuts (Ctrl+Z/Y/C/X/V) are also handled here so they fire when the OS delivers the key event; Update() handles the rest.</summary>
    private void OnKeyDown(object? sender, InputKeyEventArgs e)
    {
        if (e.Key == Keys.None) return;
        Input.OnKeyDown(e.Key);

        // Edit shortcuts in KeyDown so they work when OS/SDL delivers the event (Ctrl+Z/C/X/V are often consumed before next poll)
        bool dialogsClosed = !_openDialogPanel.IsVisible && !_optionsDialogPanel.IsVisible;
        if (!dialogsClosed) return;
        var (ctrl, shift) = InputManager.GetModifierKeysDown();
        if (!ctrl) return;

        switch (e.Key)
        {
            case Keys.Z:
                if (shift) CommandStack.Redo(); else CommandStack.Undo();
                break;
            case Keys.Y:
                CommandStack.Redo();
                break;
            case Keys.C:
                _eventConsolePanel.CopySelectionToClipboard();
                break;
            case Keys.X:
                if (_eventConsolePanel.CopySelectionToClipboard()) _eventConsolePanel.RemoveSelectedEntry();
                break;
            case Keys.V:
                // Paste: no-op
                break;
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
                ProjectPersistence.ApplyState(saved, Project, Transport, TimelineView);
                _playheadDisplayTime = Transport.CurrentTime;
                ApplyLayoutFromSaved(saved);
                EnsureDefaultTracks();
                _currentProjectPath = path;
                ResolveProjectAudioPath();
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
                ProjectPersistence.ApplyState(saved, Project, Transport, TimelineView);
                _playheadDisplayTime = Transport.CurrentTime;
                ApplyLayoutFromSaved(saved);
                EnsureDefaultTracks();
                _currentProjectPath = path;
                ResolveProjectAudioPath();
                ProjectPersistence.AddRecentProjectPath(path);
                if (!string.IsNullOrWhiteSpace(Project.AudioFilePath))
                    _loadSavedAudioOnStart = true;
            }
            return;
        }
        _openDialogPanel.ClearResult();
    }

    private void RemoveTracksImmediate(IReadOnlyList<EventTrackBase> tracks)
    {
        if (tracks == null || tracks.Count == 0) return;
        foreach (var t in tracks)
        {
            if (Project.EventTracks.Contains(t))
                Project.EventTracks.Remove(t);
        }
        Selection.RemoveTracksFromSelection(tracks);
        Selection.SelectedEventTime = null;
        if (Selection.SelectedEventTrack == null && Project.EventTracks.Count > 0)
            Selection.SelectedEventTrack = Project.EventTracks[0];
    }

    /// <summary>Handles result after the options dialog is closed.</summary>
    private void ApplyOptionsDialogResult()
    {
        _optionsDialogPanel.ClearResult();
        _pendingDeleteTrack = null;
        _pendingDeleteTracks = null;
    }

    /// <summary>If current project path is set and AudioFilePath is relative, resolve it relative to the project folder.</summary>
    private void ResolveProjectAudioPath()
    {
        if (string.IsNullOrWhiteSpace(_currentProjectPath) || string.IsNullOrWhiteSpace(Project.AudioFilePath)) return;
        if (Path.IsPathRooted(Project.AudioFilePath)) return;
        string? projectDir = Path.GetDirectoryName(_currentProjectPath);
        if (string.IsNullOrEmpty(projectDir)) return;
        Project.AudioFilePath = Path.Combine(projectDir, Project.AudioFilePath);
    }

    private void UpdateWindowTitle()
    {
        if (string.IsNullOrWhiteSpace(_currentProjectPath))
            Window.Title = "AGX Beat Sync";
        else
            Window.Title = "AGX Beat Sync - " + Path.GetFileNameWithoutExtension(_currentProjectPath);
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
        try
        {
            var (target, oyaw, opitch, odist) = _gameViewPanel.GetCameraState();
            string actualPath = ProjectPersistence.SaveToFile(Project, Transport, path, TimelineView, _layout.GameViewHeightPx, _layout.GameViewWidthPx, _layout.InspectorWidthPx,
                target.X, target.Y, target.Z, oyaw, opitch, odist);
            _currentProjectPath = actualPath;
            ProjectPersistence.AddRecentProjectPath(actualPath);
            _pendingSavedPath = actualPath;
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
            var (target, oyaw, opitch, odist) = _gameViewPanel.GetCameraState();
            string actualPath = ProjectPersistence.SaveToFile(Project, Transport, _currentProjectPath, TimelineView, _layout.GameViewHeightPx, _layout.GameViewWidthPx, _layout.InspectorWidthPx,
                target.X, target.Y, target.Z, oyaw, opitch, odist);
            _currentProjectPath = actualPath;
            _pendingSavedPath = actualPath;
        }
        catch
        {
            // Could show a message; for now fail silently
        }
    }

    /// <summary>Applies saved layout and camera from a loaded session state, clamped to current window.</summary>
    private void ApplyLayoutFromSaved(SavedSessionState saved)
    {
        int maxH = Math.Max(PanelLayout.MinGameViewHeight, _graphics.PreferredBackBufferHeight - PanelLayout.HeaderBarHeight - PanelLayout.TransportHeight - PanelLayout.TimelineStripsHeight - PanelLayout.MinTimelineHeight - PanelLayout.StatusBarHeight);
        _layout.GameViewHeightPx = Math.Clamp(saved.GameViewHeightPx, PanelLayout.MinGameViewHeight, maxH);
        if (saved.GameViewWidthPx != 0)
        {
            int maxW = Math.Max(PanelLayout.MinGameViewWidth, _graphics.PreferredBackBufferWidth - PanelLayout.MinEventConsoleWidth);
            _layout.GameViewWidthPx = Math.Clamp(saved.GameViewWidthPx, PanelLayout.MinGameViewWidth, maxW);
        }
        if (saved.InspectorWidthPx != 0)
        {
            int maxW = Math.Max(PanelLayout.MinInspectorWidth, _graphics.PreferredBackBufferWidth - PanelLayout.TrackListWidth - PanelLayout.MinCenterWidth);
            _layout.InspectorWidthPx = Math.Clamp(saved.InspectorWidthPx, PanelLayout.MinInspectorWidth, maxW);
        }
        if (saved.CameraOrbitDistance >= 1f)
        {
            _gameViewPanel.SetCameraState(
                new Microsoft.Xna.Framework.Vector3(saved.CameraTargetX, saved.CameraTargetY, saved.CameraTargetZ),
                saved.CameraOrbitYaw, saved.CameraOrbitPitch, saved.CameraOrbitDistance);
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
        Transport.BeatOffsetSeconds = Project.InTime ?? 0;

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
        // On Windows always merge Win32 key state so shortcuts see keys even when MonoGame/SDL polling or KeyDown/KeyUp fail. Focus check (shortcutsAllowed) still prevents acting when alt-tabbed.
        bool gameWindowHasFocus = IsActive || IsGameWindowForeground();
        Input.Update(OperatingSystem.IsWindows() ? true : gameWindowHasFocus);

        // Global shortcuts only when no modal dialog and window has focus (Space, Delete, brackets, Ctrl+1/2, Ctrl+Z/Y/C/X/V/S)
        bool shortcutsAllowed = !_openDialogPanel.IsVisible && !_optionsDialogPanel.IsVisible && gameWindowHasFocus;
        if (shortcutsAllowed && Input.IsKeyPressed(Keys.Space))
        {
            if (Transport.IsPlaying)
            {
                Transport.Pause();
                _audio.Pause();
            }
            else
            {
                InspectorDrawer.InvalidateLabelCache();
                Transport.Play();
                if (_audio.LoadedFilePath != null)
                {
                    _audio.StopOutputOnly();
                    _audio.Seek(Transport.CurrentTime);
                    _audio.Play();
                }
            }
        }
        if (shortcutsAllowed && Input.IsKeyPressed(Keys.Delete))
        {
            if (Selection.SelectedNotes.Count > 0)
            {
                var toRemove = Selection.SelectedNotes.ToList();
                foreach (var (track, eventTime) in toRemove)
                {
                    if (track is EventTrackBase baseTrack && Project.EventTracks.Contains(baseTrack))
                    {
                        baseTrack.EventTimes.Remove(eventTime);
                        baseTrack.EventDurations.Remove(eventTime);
                    }
                }
                Selection.SetSelectedNotes(Array.Empty<(IEventTrack, double)>());
            }
            else
            {
                var trackList = Selection.SelectedEventTracks
                    .Where(t => t is EventTrackBase b && Project.EventTracks.Contains(b))
                    .Cast<EventTrackBase>()
                    .ToList();
                if (trackList.Count > 0)
                    RemoveTracksImmediate(trackList);
            }
        }
        bool shift = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
        bool ctrl = Input.IsKeyDown(Keys.LeftControl) || Input.IsKeyDown(Keys.RightControl);
        // BPM: [ and ] to decrease/increase (hold Shift for ±5)
        double bpmStep = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift) ? 5.0 : 1.0;
        if (shortcutsAllowed && Input.IsKeyPressed(Keys.OemCloseBrackets))
        {
            Transport.BPM = Math.Min(999, Transport.BPM + bpmStep);
            Project.BPM = (float)Transport.BPM;
        }
        if (shortcutsAllowed && Input.IsKeyPressed(Keys.OemOpenBrackets))
        {
            Transport.BPM = Math.Max(20, Transport.BPM - bpmStep);
            Project.BPM = (float)Transport.BPM;
        }
        // Grid size (Ableton-style): Ctrl+1 = finer, Ctrl+2 = coarser
        if (shortcutsAllowed && ctrl && Input.IsKeyPressed(Keys.D1))
        {
            TimelineView.GridSubdivisionsPerBeat = Math.Min(TimelineViewState.MaxGridSubdivisions, TimelineView.GridSubdivisionsPerBeat * 2);
        }
        if (shortcutsAllowed && ctrl && Input.IsKeyPressed(Keys.D2))
        {
            TimelineView.GridSubdivisionsPerBeat = Math.Max(TimelineViewState.MinGridSubdivisions, TimelineView.GridSubdivisionsPerBeat / 2);
        }

        // Record mode: R toggles; when playing, 1-9 and 0 add event to track 1-10 at current time (only when Ctrl not held)
        if (shortcutsAllowed && !ctrl && Input.IsKeyPressed(Keys.R))
            _recordMode = !_recordMode;

        // Edit shortcuts: Ctrl+S here; Ctrl+Z/Y/C/X/V handled in OnKeyDown so they fire when OS delivers the key
        if (shortcutsAllowed && ctrl && Input.IsKeyPressed(Keys.S)) { if (shift) SaveProjectAsDialogOnStaThread(); else SaveProjectToCurrentPath(); }

        // Seek backward: clear fired events and spawned entities so replay is correct
        if (Transport.CurrentTime < _lastPlaybackTime)
        {
            _eventFiredSet.Clear();
            _gameViewPanel.ClearSpawnedEntities();
            _eventConsolePanel.Clear();
        }

        // Drive transport from audio when playing with a loaded file, else game time
        double prevTime = Transport.CurrentTime;
        if (Transport.IsPlaying)
        {
            if (_audio.LoadedFilePath != null && _audio.IsPlaying)
                Transport.CurrentTime = _audio.CurrentTimeSeconds;
            else if (_audio.LoadedFilePath == null)
                Transport.CurrentTime += gameTime.ElapsedGameTime.TotalSeconds;

            if (Project.InTime is { } inT && Project.OutTime is { } outT && outT > inT)
            {
                const double inOutTolerance = 0.05; // avoid stopping when starting at in point (audio may report slightly before inT for a frame)
                bool pastEnd = Transport.CurrentTime > outT;
                bool beforeStart = Transport.CurrentTime < inT - inOutTolerance;
                if (pastEnd || beforeStart)
                {
                    Transport.Pause();
                    _audio.Pause();
                    Transport.CurrentTime = Math.Clamp(Transport.CurrentTime, inT, outT);
                }
                else
                    Transport.CurrentTime = Math.Clamp(Transport.CurrentTime, inT, outT);
            }

            if (Transport.IsPlaying)
            {
                const double playheadSmooth = 0.35;
                _playheadDisplayTime += (Transport.CurrentTime - _playheadDisplayTime) * playheadSmooth;
            }
            else
                _playheadDisplayTime = Transport.CurrentTime;
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
                    // Fire when playhead crosses event time (include event at start: prevTime and eventTime both 0)
                    bool crossed = eventTime <= Transport.CurrentTime && (eventTime > prevTime || (prevTime == 0 && eventTime == 0));
                    if (crossed && _eventFiredSet.Add((ti, eventTime)))
                    {
                        string message = track switch
                        {
                            SpawnEntityTrack => "spawned entity",
                            ChangeEntityColorTrack => "changed entity color",
                            _ => "fired"
                        };
                        _eventConsolePanel.LogEvent(Transport.CurrentTime, track.DisplayName, message);
                        if (track is ChangeEntityColorTrack colorTrack)
                        {
                            var xnaColor = ChangeEntityColorTrack.ToXnaColor(colorTrack.GetColor(eventTime));
                            _gameViewPanel.SetEnemyCubeColor(xnaColor);
                        }
                        else if (track is SpawnEntityTrack spawnTrack)
                        {
                            var basePos = spawnTrack.PositionMode == PositionMode.Origin ? new Vector3(0, 1, 0)
                                : spawnTrack.PositionMode == PositionMode.Absolute ? spawnTrack.PositionAbsolute
                                : spawnTrack.PositionRelative;
                            var playerPos = _gameViewPanel.GetPlayerPosition();
                            var baseDir = spawnTrack.RotationMode == RotationMode.Towards
                                ? (playerPos - basePos).LengthSquared() < 1e-12f ? -Vector3.UnitZ : Vector3.Normalize(playerPos - basePos)
                                : ForwardFromEuler(spawnTrack.RotationEuler);
                            var lifetime = spawnTrack.Lifetime;
                            var speed = spawnTrack.Speed;

                            var spawns = ExpandSpawnPattern(spawnTrack, basePos, baseDir);
                            foreach (var (pos, dir) in spawns)
                                _gameViewPanel.SpawnEntityWithDirection(pos, dir, speed, lifetime,
                                    spawnTrack.DirectionPattern, spawnTrack.OscillationAmplitude, spawnTrack.OrbitingDistance);
                        }
                    }
                }
            }
        }
        _lastPlaybackTime = Transport.CurrentTime;

        // Record mode: 1-9 and 0 add event at current time to track 1-10 while playing (Ctrl not held so Ctrl+1/2 still work)
        if (Transport.IsPlaying && _recordMode && !ctrl && Project.EventTracks.Count > 0)
        {
            int? trackIndex = null;
            if (Input.IsKeyPressed(Keys.D1)) trackIndex = 0;
            else if (Input.IsKeyPressed(Keys.D2)) trackIndex = 1;
            else if (Input.IsKeyPressed(Keys.D3)) trackIndex = 2;
            else if (Input.IsKeyPressed(Keys.D4)) trackIndex = 3;
            else if (Input.IsKeyPressed(Keys.D5)) trackIndex = 4;
            else if (Input.IsKeyPressed(Keys.D6)) trackIndex = 5;
            else if (Input.IsKeyPressed(Keys.D7)) trackIndex = 6;
            else if (Input.IsKeyPressed(Keys.D8)) trackIndex = 7;
            else if (Input.IsKeyPressed(Keys.D9)) trackIndex = 8;
            else if (Input.IsKeyPressed(Keys.D0)) trackIndex = 9;
            if (trackIndex.HasValue && trackIndex.Value < Project.EventTracks.Count)
            {
                var track = Project.EventTracks[trackIndex.Value];
                double t = Transport.CurrentTime;
                const double epsilon = 0.02;
                bool already = track.EventTimes.Any(ex => Math.Abs(ex - t) < epsilon);
                if (!already)
                {
                    track.EventTimes.Add(t);
                    track.EventTimes.Sort();
                }
            }
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
            int maxH = Math.Max(minH, _graphics.PreferredBackBufferHeight - PanelLayout.HeaderBarHeight - PanelLayout.TransportHeight - PanelLayout.TimelineStripsHeight - PanelLayout.MinTimelineHeight - PanelLayout.StatusBarHeight);
            int newH = _gameViewResizeStartHeight + (_gameViewResizeStartY - Input.MousePosition.Y);
            _layout.GameViewHeightPx = Math.Clamp(newH, minH, maxH);
            if (Input.MouseLeftReleased)
                _gameViewResizeDragging = false;
        }

        _layout.Update(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);

        if (_bottomRowResizeDragging)
        {
            int minW = PanelLayout.MinGameViewWidth;
            int maxW = Math.Max(minW, _graphics.PreferredBackBufferWidth - PanelLayout.MinEventConsoleWidth);
            int newW = _bottomRowResizeStartWidth + (Input.MousePosition.X - _bottomRowResizeStartX);
            _layout.GameViewWidthPx = Math.Clamp(newW, minW, maxW);
            if (Input.MouseLeftReleased)
                _bottomRowResizeDragging = false;
        }

        if (_inspectorResizeDragging)
        {
            int minW = PanelLayout.MinInspectorWidth;
            int maxW = Math.Max(minW, _graphics.PreferredBackBufferWidth - PanelLayout.TrackListWidth - PanelLayout.MinCenterWidth);
            int newW = _inspectorResizeStartWidth + (_inspectorResizeStartX - Input.MousePosition.X);
            _layout.InspectorWidthPx = Math.Clamp(newW, minW, maxW);
            if (Input.MouseLeftReleased)
                _inspectorResizeDragging = false;
        }

        if (!_gameViewResizeDragging && !_bottomRowResizeDragging && !_inspectorResizeDragging && Input.MouseLeftPressed && _layout.BottomRowDividerGrip.Contains(Input.MousePosition))
        {
            _bottomRowResizeDragging = true;
            _bottomRowResizeStartX = Input.MousePosition.X;
            _bottomRowResizeStartWidth = _layout.GameView.Width;
            if (_layout.GameViewWidthPx == 0)
                _layout.GameViewWidthPx = _bottomRowResizeStartWidth;
        }

        _openDialogPanel.GraphicsDevice = GraphicsDevice;
        _openDialogPanel.Input = Input;
        if (_openDialogPanel.IsVisible)
        {
            _openDialogPanel.Update(gameTime, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
            if (!_openDialogPanel.IsVisible)
                ApplyOpenDialogResult();
            UpdateWindowTitle();
            UpdateMetrics(gameTime);
            base.Update(gameTime);
            return;
        }

        _optionsDialogPanel.GraphicsDevice = GraphicsDevice;
        _optionsDialogPanel.Input = Input;
        if (_optionsDialogPanel.IsVisible)
        {
            _optionsDialogPanel.Update(gameTime, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
            if (!_optionsDialogPanel.IsVisible)
                ApplyOptionsDialogResult();
            UpdateWindowTitle();
            UpdateMetrics(gameTime);
            base.Update(gameTime);
            return;
        }

        if (!_gameViewResizeDragging && !_inspectorResizeDragging && Input.MouseLeftPressed && _layout.DividerGrip.Contains(Input.MousePosition))
        {
            _gameViewResizeDragging = true;
            _gameViewResizeStartY = Input.MousePosition.Y;
            _gameViewResizeStartHeight = _layout.GameViewHeightPx;
        }

        if (!_gameViewResizeDragging && !_bottomRowResizeDragging && !_inspectorResizeDragging && Input.MouseLeftPressed && _layout.InspectorDividerGrip.Contains(Input.MousePosition))
        {
            _inspectorResizeDragging = true;
            _inspectorResizeStartX = Input.MousePosition.X;
            _inspectorResizeStartWidth = _layout.Inspector.Width;
            if (_layout.InspectorWidthPx == 0)
                _layout.InspectorWidthPx = _inspectorResizeStartWidth;
        }
        _headerBarPanel.Bounds = _layout.HeaderBar;
        _headerBarPanel.GraphicsDevice = GraphicsDevice;
        _headerBarPanel.Input = Input;
        _headerBarPanel.OnSave = SaveProjectToCurrentPath;
        _headerBarPanel.OnSaveAs = SaveProjectAsDialogOnStaThread;
        _headerBarPanel.OnImportMusic = () =>
        {
            string? path = null;
            var t = new Thread(() => { try { path = AudioImportService.PickAudioFile(); } catch { } });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            RestoreWindowFocus();
            if (path != null)
                StartAudioLoad(path, detectBpm: true);
        };
        _headerBarPanel.OnOpenProject = () =>
        {
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
                ProjectPersistence.ApplyState(saved, Project, Transport, TimelineView);
                _playheadDisplayTime = Transport.CurrentTime;
                ApplyLayoutFromSaved(saved);
                EnsureDefaultTracks();
                _currentProjectPath = path;
                ResolveProjectAudioPath();
                ProjectPersistence.AddRecentProjectPath(path);
                if (!string.IsNullOrWhiteSpace(Project.AudioFilePath))
                    _loadSavedAudioOnStart = true;
            }
        };
        _headerBarPanel.OnRecentProjects = () => _openDialogPanel.Open();
        _headerBarPanel.OnUndo = () => CommandStack.Undo();
        _headerBarPanel.OnRedo = () => CommandStack.Redo();
        _headerBarPanel.OnCut = () =>
        {
            if (_eventConsolePanel.CopySelectionToClipboard())
                _eventConsolePanel.RemoveSelectedEntry();
        };
        _headerBarPanel.OnCopy = () => _eventConsolePanel.CopySelectionToClipboard();
        _headerBarPanel.OnPaste = () => { /* Paste: no-op for now */ };
        _headerBarPanel.Update(gameTime);
        _transportBar.Bounds = _layout.TransportBar;
        _trackListPanel.Bounds = _layout.TrackList;
        _trackListPanel.Project = Project;
        _trackListPanel.Selection = Selection;
        _trackListPanel.Input = Input;
        _trackListPanel.RecordMode = RecordMode;
        _trackListPanel.OnDeleteTrackRequested = track => RemoveTracksImmediate(new[] { track });
        _transportBar.Project = Project;
        _transportBar.Transport = Transport;
        _transportBar.Input = Input;
        _transportBar.RecordMode = RecordMode;
        _transportBar.OnRecordToggle = () => _recordMode = !_recordMode;
        _transportBar.OnPlayPauseToggle = () =>
        {
            if (Transport.IsPlaying)
            {
                Transport.Pause();
                _audio.Pause();
            }
            else
            {
                InspectorDrawer.InvalidateLabelCache();
                Transport.Play();
                if (_audio.LoadedFilePath != null)
                {
                    _audio.StopOutputOnly();
                    _audio.Seek(Transport.CurrentTime);
                    _audio.Play();
                }
            }
        };
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
                double t = result.Value;
                if (Project.InTime is { } inT && Project.OutTime is { } outT && outT > inT)
                    t = Math.Clamp(t, inT, outT);
                Transport.Seek(t);
                _playheadDisplayTime = t;
                if (_audio.LoadedFilePath != null)
                    _audio.Seek(t);
            }
        };
        _transportBar.Update(gameTime);
        _timelinePanel.Bounds = _layout.Timeline;
        _timelinePanel.IgnoreClickRect = _layout.DividerGrip;

        UpdateWindowTitle();
        _inspectorPanel.Bounds = _layout.Inspector;
        _eventConsolePanel.Bounds = _layout.EventConsole;
        _eventConsolePanel.Input = Input;
        _gameViewPanel.Bounds = _layout.GameView;
        _gameViewPanel.Input = Input;
        _gameViewPanel.Project = Project;
        _gameViewPanel.Transport = Transport;

        Transport.BeatOffsetSeconds = Project.InTime ?? 0;
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
        _inspectorPanel.Project = Project;

        _statusBarPanel.Bounds = _layout.StatusBar;
        string? hoverText = null;
        MouseCursor? desiredCursor = null;
        if (_inspectorResizeDragging)
            desiredCursor = MouseCursor.SizeWE;
        else if (_bottomRowResizeDragging)
            desiredCursor = MouseCursor.SizeWE;
        else if (_gameViewResizeDragging)
            desiredCursor = MouseCursor.SizeNS;
        else if (_headerBarPanel.ContainsPoint(Input.MousePosition))
            hoverText = _headerBarPanel.GetHoverText(Input.MousePosition);
        else if (_layout.DividerGrip.Contains(Input.MousePosition))
        {
            hoverText = "Drag to resize timeline / game view";
            desiredCursor = MouseCursor.SizeNS;
        }
        else if (_layout.BottomRowDividerGrip.Contains(Input.MousePosition))
        {
            hoverText = "Drag to resize game view / event console";
            desiredCursor = MouseCursor.SizeWE;
        }
        else if (_layout.InspectorDividerGrip.Contains(Input.MousePosition))
        {
            hoverText = "Drag to resize inspector";
            desiredCursor = MouseCursor.SizeWE;
        }
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
        else if (_eventConsolePanel.ContainsPoint(Input.MousePosition))
            hoverText = "Event Console — click to select, Ctrl+C to copy";
        else if (_gameViewPanel.ContainsPoint(Input.MousePosition))
            hoverText = _gameViewPanel.GetHoverText(Input.MousePosition);

        double totalSeconds = gameTime.TotalGameTime.TotalSeconds;
        if (_pendingSavedPath != null)
        {
            _statusBarSavedMessage = "Saved to " + _pendingSavedPath;
            _statusBarSavedMessageUntil = totalSeconds + 3;
            _pendingSavedPath = null;
        }
        if (_statusBarSavedMessageUntil.HasValue && totalSeconds >= _statusBarSavedMessageUntil.Value)
        {
            _statusBarSavedMessage = null;
            _statusBarSavedMessageUntil = null;
        }
        string? volumeStatus = _transportBar.GetVolumeStatusText(totalSeconds);
        _statusBarPanel.HoverText = (_statusBarSavedMessageUntil.HasValue ? _statusBarSavedMessage : null) ?? volumeStatus ?? hoverText ?? "";
        try { Mouse.SetCursor(desiredCursor ?? MouseCursor.Arrow); } catch { /* ignore on some platforms */ }

        _audio.Volume = _transportBar.Volume;
        _trackListPanel.Update(gameTime);
        _timelinePanel.Update(gameTime);
        _inspectorPanel.Update(gameTime);
        _eventConsolePanel.Update(gameTime);
        _gameViewPanel.Update(gameTime);
        IsMouseVisible = !_gameViewPanel.IsCapturingMouse;

        UpdateMetrics(gameTime);
        base.Update(gameTime);
    }

    private static Vector3 ForwardFromEuler(Vector3 eulerRadians)
    {
        var rot = Matrix.CreateRotationX(eulerRadians.X) * Matrix.CreateRotationY(eulerRadians.Y) * Matrix.CreateRotationZ(eulerRadians.Z);
        var f = Vector3.Transform(-Vector3.UnitZ, rot);
        return f.LengthSquared() > 1e-8f ? Vector3.Normalize(f) : -Vector3.UnitZ;
    }

    private static List<(Vector3 position, Vector3 direction)> ExpandSpawnPattern(SpawnEntityTrack t, Vector3 basePos, Vector3 baseDir)
    {
        var list = new List<(Vector3 position, Vector3 direction)>();
        int n = t.SpawnMode == SpawnMode.Single ? 1 : Math.Clamp(t.Count, 1, 10);
        if (n == 1)
        {
            list.Add((basePos, baseDir));
            return list;
        }
        switch (t.Pattern)
        {
            case SpawnPattern.Circle:
            {
                float radius = Math.Max(0f, t.CircleRadius);
                float spanRad = t.CircleFullCircle ? MathF.PI * 2f : (t.CircleSpread * MathF.PI / 180f);
                float start = t.CircleFullCircle ? 0f : -spanRad * 0.5f;
                for (int i = 0; i < n; i++)
                {
                    float t0 = n > 1 ? (i / (float)(n - 1)) : 0.5f;
                    float angle = start + t0 * spanRad;
                    var dir = new Vector3(MathF.Sin(angle), 0, -MathF.Cos(angle));
                    var pos = basePos + dir * radius;
                    list.Add((pos, dir));
                }
                break;
            }
            case SpawnPattern.Cone:
            {
                float halfRad = Math.Clamp(t.ConeSpreadAngle * 0.5f, 0.01f, 179f) * MathF.PI / 180f;
                var right = Vector3.Cross(baseDir, Vector3.UnitY);
                if (right.LengthSquared() < 1e-10f) right = Vector3.UnitX;
                else right.Normalize();
                for (int i = 0; i < n; i++)
                {
                    float t0 = n > 1 ? (i / (float)(n - 1)) * 2f - 1f : 0f;
                    float angle = t0 * halfRad;
                    var dir = Vector3.Normalize(baseDir * MathF.Cos(angle) + right * MathF.Sin(angle));
                    list.Add((basePos, dir));
                }
                break;
            }
            case SpawnPattern.Line:
            {
                float len = Math.Max(0.001f, t.LineLength);
                var right = Vector3.Cross(baseDir, Vector3.UnitY);
                if (right.LengthSquared() < 1e-10f) right = Vector3.UnitX;
                else right.Normalize();
                for (int i = 0; i < n; i++)
                {
                    float t0 = n > 1 ? (i / (float)(n - 1)) - 0.5f : 0f;
                    var pos = basePos + right * (t0 * len);
                    list.Add((pos, baseDir));
                }
                break;
            }
        }
        return list;
    }

    private void UpdateMetrics(GameTime gameTime)
    {
        _metricsFrameCount++;
        _metricsSampleAccum += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_metricsSampleAccum < MetricsSampleInterval)
            return;
        _metricsFps = _metricsFrameCount / _metricsSampleAccum;
        _metricsFrameCount = 0;
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

        // CullNone so that when InspectorPanel flushes the batch mid-frame (End/Begin for scissor), the flush doesn't cull 2D sprites
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, RasterizerState.CullNone);

        // Draw inspector first so its mid-frame End/RT/Begin (when a track is selected) flushes an empty batch; transport, track list, timeline are then drawn and flushed only at the final End(), avoiding a black timeline
        _inspectorPanel.Draw(_spriteBatch);
        _transportBar.Draw(_spriteBatch);
        _trackListPanel.Draw(_spriteBatch);
        _timelinePanel.Draw(_spriteBatch);

        // Draw bottom-row dividers: horizontal bar only over game view so console area has no bar; vertical bar under both panels
        var gripPixel = PanelBase.GetPixelTexture(GraphicsDevice);
        var grip = _layout.DividerGrip;
        var gameView = _layout.GameView;
        var horizontalBarY = grip.Y;
        var horizontalBarHeight = grip.Height;
        _spriteBatch.Draw(gripPixel, new Rectangle(gameView.X, horizontalBarY, gameView.Width, horizontalBarHeight), new Color(70, 75, 85));
        _spriteBatch.Draw(gripPixel, new Rectangle(gameView.X, horizontalBarY + horizontalBarHeight / 2 - 1, gameView.Width, 2), new Color(110, 118, 132));
        var bottomGrip = _layout.BottomRowDividerGrip;
        _spriteBatch.Draw(gripPixel, bottomGrip, new Color(70, 75, 85));
        _spriteBatch.Draw(gripPixel, new Rectangle(bottomGrip.X + bottomGrip.Width / 2 - 1, bottomGrip.Y, 2, bottomGrip.Height), new Color(110, 118, 132));

        _eventConsolePanel.Draw(_spriteBatch);

        if (_audioLoad.IsLoading)
        {
            var pixel = PanelBase.GetPixelTexture(GraphicsDevice);
            LoadingOverlay.Draw(_spriteBatch, pixel, GraphicsDevice, _audioLoad.Progress, gameTime);
        }

        _gameViewPanel.Draw3DScene();
        _gameViewPanel.Draw(_spriteBatch);

        var inspectorGrip = _layout.InspectorDividerGrip;
        _spriteBatch.Draw(gripPixel, inspectorGrip, new Color(70, 75, 85));
        _spriteBatch.Draw(gripPixel, new Rectangle(inspectorGrip.X + inspectorGrip.Width / 2 - 1, inspectorGrip.Y, 2, inspectorGrip.Height), new Color(110, 118, 132));

        // Draw header (and File dropdown) on top so dropdown is not covered by transport/timeline
        _headerBarPanel.Draw(_spriteBatch);

        DrawMetricsOverlay(gameTime);
        _statusBarPanel.Draw(_spriteBatch);

        if (_openDialogPanel.IsVisible)
            _openDialogPanel.Draw(_spriteBatch);
        if (_optionsDialogPanel.IsVisible)
            _optionsDialogPanel.Draw(_spriteBatch);

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void DrawMetricsOverlay(GameTime gameTime)
    {
        int viewW = GraphicsDevice.Viewport.Width;
        string s = $"{(int)Math.Round(_metricsFps)} FPS  CPU: {_metricsCpuPercent:F1}%  Mem: {_metricsMemoryMb} MB";
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
        try { Window.KeyDown -= OnKeyDown; } catch { }
        _audio.Dispose();
        base.UnloadContent();
    }
}
