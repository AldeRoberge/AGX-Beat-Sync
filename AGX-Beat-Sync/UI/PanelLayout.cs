using Microsoft.Xna.Framework;

namespace AGX_Beat_Sync.UI;

/// <summary>
/// Computes panel bounds for the main editor layout.
/// Layout: Header bar (top) | Transport bar | Track list (left) | Timeline (center) | Inspector (right, full height).
/// Bottom row: Game View (left) | Event Console (right). Both share the row height; game view width is user-adjustable via the divider.
/// </summary>
public class PanelLayout
{
    public const int HeaderBarHeight = 28;
    public const int TransportHeight = 36;
    public const int TrackListWidth = 200;
    public const int InspectorWidth = 280;
    public const int MinInspectorWidth = 180;
    public const int MinCenterWidth = 150;
    public const int MinInspectorHeight = 100;
    public const int MinEventConsoleHeight = 80;
    /// <summary>Default width of the event console when GameViewWidthPx is not set (bottom row).</summary>
    public const int DefaultEventConsoleWidth = 220;
    /// <summary>Default height of the bottom row (game view + event console).</summary>
    public const int DefaultGameViewHeight = 220;
    public const int MinGameViewHeight = 80;
    public const int MinGameViewWidth = 200;
    public const int MinEventConsoleWidth = 150;
    public const int MinTimelineHeight = 100;
    /// <summary>Height reserved at top of timeline for in/out strip + playhead strip (panel extends up by this much so tracks are not offset).</summary>
    public const int TimelineStripsHeight = 35;
    public const int StatusBarHeight = 26;

    /// <summary>Height of the game view panel in pixels. User can resize by dragging the divider.</summary>
    public int GameViewHeightPx { get; set; } = DefaultGameViewHeight;

    /// <summary>Width of the inspector panel in pixels. 0 = use InspectorWidth (280).</summary>
    public int InspectorWidthPx { get; set; }

    /// <summary>Width of the game view panel in pixels (bottom row). 0 = use full width to the left of inspector.</summary>
    public int GameViewWidthPx { get; set; }

    public Rectangle HeaderBar { get; private set; }
    public Rectangle TransportBar { get; private set; }
    public Rectangle TrackList { get; private set; }
    public Rectangle Timeline { get; private set; }
    public Rectangle Inspector { get; private set; }
    public Rectangle EventConsole { get; private set; }
    public Rectangle GameView { get; private set; }
    public Rectangle StatusBar { get; private set; }

    /// <summary>Rectangle to hit-test for dragging the timeline/game view divider (top edge of bottom row). Spans full width to the left of inspector.</summary>
    public Rectangle DividerGrip => new(0, GameView.Y - DividerGripHalfHeight, GameView.Width + EventConsole.Width, DividerGripHalfHeight * 2);

    /// <summary>Rectangle to hit-test for dragging the timeline / inspector divider (left edge of right column). Spans full column height (inspector).</summary>
    public Rectangle InspectorDividerGrip => new(Inspector.X - InspectorGripHalfWidth, Inspector.Y, InspectorGripHalfWidth * 2, Inspector.Height);

    /// <summary>Rectangle to hit-test for dragging the vertical divider between game view and event console (left edge of event console).</summary>
    public Rectangle BottomRowDividerGrip => new(EventConsole.X - DividerGripHalfHeight, EventConsole.Y, DividerGripHalfHeight * 2, EventConsole.Height);

    public const int DividerGripHalfHeight = 4;
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

        int maxGameViewHeight = Math.Max(MinGameViewHeight, mainHeight - MinTimelineHeight);
        int bottomPanelHeight = Math.Clamp(GameViewHeightPx, MinGameViewHeight, maxGameViewHeight);
        GameViewHeightPx = bottomPanelHeight;

        int bottomRowY = h - StatusBarHeight - bottomPanelHeight;
        int rightColumnHeight = bottomRowY - mainAreaTop;
        int bottomRowWidth = w - inspectorWidth;

        // Inspector takes full right column above the bottom row
        Inspector = new Rectangle(w - inspectorWidth, mainAreaTop, inspectorWidth, rightColumnHeight);

        // Bottom row: Game View (left) | Event Console (right, extends under inspector to fill window)
        int gameViewWidth;
        int eventConsoleWidth;
        if (GameViewWidthPx > 0)
        {
            gameViewWidth = Math.Clamp(GameViewWidthPx, MinGameViewWidth, bottomRowWidth - MinEventConsoleWidth);
            eventConsoleWidth = bottomRowWidth - gameViewWidth;
        }
        else
        {
            eventConsoleWidth = Math.Clamp(DefaultEventConsoleWidth, MinEventConsoleWidth, bottomRowWidth - MinGameViewWidth);
            gameViewWidth = bottomRowWidth - eventConsoleWidth;
        }
        GameView = new Rectangle(0, bottomRowY, gameViewWidth, bottomPanelHeight);
        EventConsole = new Rectangle(gameViewWidth, bottomRowY, w - gameViewWidth, bottomPanelHeight);

        int centerHeight = mainHeight - bottomPanelHeight;
        int centerWidth = w - inspectorWidth - TrackListWidth;
        // Timeline starts in the strips band (below transport); strips sit above track list/inspector
        Timeline = new Rectangle(TrackListWidth, mainTop - TimelineStripsHeight, centerWidth, centerHeight + TimelineStripsHeight);
        // Track list same height as timeline so track rows align with piano roll lanes
        TrackList = new Rectangle(0, mainAreaTop, TrackListWidth, centerHeight + TimelineStripsHeight);

        StatusBar = new Rectangle(0, h - StatusBarHeight, w, StatusBarHeight);
    }
}
