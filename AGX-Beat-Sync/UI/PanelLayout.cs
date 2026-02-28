using Microsoft.Xna.Framework;

namespace AGX_Beat_Sync.UI;

/// <summary>
/// Computes panel bounds for the main editor layout.
/// Layout: Header bar (top) | Transport bar | Track list (left) | Timeline (center) | Inspector (right).
/// Bottom row: Game View (left) | Event Console (right). Game view height is user-adjustable via the divider above it.
/// </summary>
public class PanelLayout
{
    public const int HeaderBarHeight = 28;
    public const int TransportHeight = 32;
    public const int TrackListWidth = 200;
    public const int InspectorWidth = 280;
    public const int MinInspectorWidth = 180;
    public const int MinCenterWidth = 150;
    /// <summary>Fraction of the bottom row width for the game view (rest is Event Console). Used when GameViewWidthPx is 0.</summary>
    private const float GameViewWidthFraction = 0.45f;
    public const int MinGameViewWidth = 220;
    public const int MinEventConsoleWidth = 200;
    /// <summary>Default height of the bottom row (game view + event console). Kept small so the timeline gets most of the vertical space.</summary>
    public const int DefaultGameViewHeight = 120;
    public const int MinGameViewHeight = 80;
    public const int MinTimelineHeight = 100;
    /// <summary>Height reserved at top of timeline for in/out strip + playhead strip (panel extends up by this much so tracks are not offset).</summary>
    public const int TimelineStripsHeight = 35;
    public const int StatusBarHeight = 26;

    /// <summary>Height of the game view panel in pixels. User can resize by dragging the divider.</summary>
    public int GameViewHeightPx { get; set; } = DefaultGameViewHeight;

    /// <summary>Width of the game view in the bottom row in pixels. 0 = use GameViewWidthFraction.</summary>
    public int GameViewWidthPx { get; set; }

    /// <summary>Width of the inspector panel in pixels. 0 = use InspectorWidth (280).</summary>
    public int InspectorWidthPx { get; set; }

    public Rectangle HeaderBar { get; private set; }
    public Rectangle TransportBar { get; private set; }
    public Rectangle TrackList { get; private set; }
    public Rectangle Timeline { get; private set; }
    public Rectangle Inspector { get; private set; }
    public Rectangle EventConsole { get; private set; }
    public Rectangle GameView { get; private set; }
    public Rectangle StatusBar { get; private set; }

    /// <summary>Rectangle to hit-test for dragging the timeline/game view divider (top edge of game view). Spans full bottom row width (game view + event console).</summary>
    public Rectangle DividerGrip => new(0, GameView.Y - DividerGripHalfHeight, GameView.Width + EventConsole.Width, DividerGripHalfHeight * 2);

    /// <summary>Rectangle to hit-test for dragging the game view / event console divider (vertical split).</summary>
    public Rectangle BottomRowDividerGrip => new(GameView.Right - BottomRowGripHalfWidth, GameView.Y, BottomRowGripHalfWidth * 2, GameView.Height);

    /// <summary>Rectangle to hit-test for dragging the timeline / inspector divider (left edge of inspector).</summary>
    public Rectangle InspectorDividerGrip => new(Inspector.X - InspectorGripHalfWidth, Inspector.Y, InspectorGripHalfWidth * 2, Inspector.Height);

    public const int DividerGripHalfHeight = 4;
    public const int BottomRowGripHalfWidth = 4;
    public const int InspectorGripHalfWidth = 4;

    public void Update(int windowWidth, int windowHeight)
    {
        int w = windowWidth;
        int h = windowHeight;

        HeaderBar = new Rectangle(0, 0, w, HeaderBarHeight);
        TransportBar = new Rectangle(0, HeaderBarHeight, w, TransportHeight);

        // Reserve space for timeline in/out + playhead strips below transport so they don't overlap header/transport
        int mainTop = HeaderBarHeight + TransportHeight + TimelineStripsHeight;
        int mainHeight = h - mainTop - StatusBarHeight;
        int mainAreaTop = mainTop - TimelineStripsHeight; // align with timeline top so no black gap
        int mainAreaHeight = mainHeight + TimelineStripsHeight;
        int maxInspectorWidth = Math.Max(MinInspectorWidth, w - TrackListWidth - MinCenterWidth);
        int inspectorWidth = InspectorWidthPx > 0
            ? Math.Clamp(InspectorWidthPx, MinInspectorWidth, maxInspectorWidth)
            : InspectorWidth;
        Inspector = new Rectangle(w - inspectorWidth, mainAreaTop, inspectorWidth, mainAreaHeight);
        TrackList = new Rectangle(0, mainAreaTop, TrackListWidth, mainAreaHeight);

        int maxGameViewHeight = Math.Max(MinGameViewHeight, mainHeight - MinTimelineHeight);
        int bottomPanelHeight = Math.Clamp(GameViewHeightPx, MinGameViewHeight, maxGameViewHeight);
        GameViewHeightPx = bottomPanelHeight;

        int bottomRowY = h - StatusBarHeight - bottomPanelHeight;
        int bottomRowWidth = w;
        int maxGameViewWidth = Math.Max(MinGameViewWidth, bottomRowWidth - MinEventConsoleWidth);
        int gameViewWidth = GameViewWidthPx > 0
            ? Math.Clamp(GameViewWidthPx, MinGameViewWidth, maxGameViewWidth)
            : Math.Max(MinGameViewWidth, (int)(bottomRowWidth * GameViewWidthFraction));
        int eventConsoleWidth = bottomRowWidth - gameViewWidth;
        GameView = new Rectangle(0, bottomRowY, gameViewWidth, bottomPanelHeight);
        EventConsole = new Rectangle(gameViewWidth, bottomRowY, eventConsoleWidth, bottomPanelHeight);

        int centerHeight = mainHeight - bottomPanelHeight;
        int centerWidth = w - inspectorWidth - TrackListWidth;
        // Timeline starts in the strips band (below transport); strips sit above track list/inspector
        Timeline = new Rectangle(TrackListWidth, mainTop - TimelineStripsHeight, centerWidth, centerHeight + TimelineStripsHeight);

        StatusBar = new Rectangle(0, h - StatusBarHeight, w, StatusBarHeight);
    }
}
