using Microsoft.Xna.Framework;

namespace AGX_Beat_Sync.UI;

/// <summary>
/// Computes panel bounds for the main editor layout.
/// Layout: Header bar (top) | Transport bar | Track list (left) | Timeline (center) | Inspector (right, top) + Event Console (right, bottom).
/// Bottom row: Game View only (full width to the left of inspector). Game view height is user-adjustable via the divider above it.
/// </summary>
public class PanelLayout
{
    public const int HeaderBarHeight = 28;
    public const int TransportHeight = 36;
    public const int TrackListWidth = 200;
    public const int InspectorWidth = 280;
    public const int MinInspectorWidth = 180;
    public const int MinCenterWidth = 150;
    /// <summary>Fraction of the right column height for the inspector (rest is Event Console).</summary>
    private const float InspectorHeightFraction = 0.6f;
    public const int MinInspectorHeight = 100;
    public const int MinEventConsoleHeight = 80;
    /// <summary>Default height of the bottom row (game view). Game view takes more space by default.</summary>
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

    /// <summary>Rectangle to hit-test for dragging the timeline/game view divider (top edge of game view). Spans full bottom row width (game view).</summary>
    public Rectangle DividerGrip => new(0, GameView.Y - DividerGripHalfHeight, GameView.Width, DividerGripHalfHeight * 2);

    /// <summary>Rectangle to hit-test for dragging the timeline / inspector divider (left edge of right column). Spans full column height (inspector + console).</summary>
    public Rectangle InspectorDividerGrip => new(Inspector.X - InspectorGripHalfWidth, Inspector.Y, InspectorGripHalfWidth * 2, Inspector.Height + EventConsole.Height);

    /// <summary>Rectangle to hit-test for dragging the bottom-row divider between game view and event console. Empty when layout has no such divider.</summary>
    public Rectangle BottomRowDividerGrip => Rectangle.Empty;

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
        int inspectorHeight = Math.Max(MinInspectorHeight, (int)(rightColumnHeight * InspectorHeightFraction));
        int consoleHeight = rightColumnHeight - inspectorHeight;
        if (consoleHeight < MinEventConsoleHeight)
        {
            inspectorHeight = rightColumnHeight - MinEventConsoleHeight;
            if (inspectorHeight < MinInspectorHeight)
            {
                inspectorHeight = MinInspectorHeight;
                consoleHeight = rightColumnHeight - inspectorHeight;
            }
            else
                consoleHeight = MinEventConsoleHeight;
        }
        Inspector = new Rectangle(w - inspectorWidth, mainAreaTop, inspectorWidth, inspectorHeight);
        EventConsole = new Rectangle(w - inspectorWidth, mainAreaTop + inspectorHeight, inspectorWidth, consoleHeight);
        TrackList = new Rectangle(0, mainAreaTop, TrackListWidth, mainAreaHeight);

        // Game view fills entire bottom row to the left of the inspector column
        GameView = new Rectangle(0, bottomRowY, w - inspectorWidth, bottomPanelHeight);

        int centerHeight = mainHeight - bottomPanelHeight;
        int centerWidth = w - inspectorWidth - TrackListWidth;
        // Timeline starts in the strips band (below transport); strips sit above track list/inspector
        Timeline = new Rectangle(TrackListWidth, mainTop - TimelineStripsHeight, centerWidth, centerHeight + TimelineStripsHeight);

        StatusBar = new Rectangle(0, h - StatusBarHeight, w, StatusBarHeight);
    }
}
