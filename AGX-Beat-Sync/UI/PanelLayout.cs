using Microsoft.Xna.Framework;

namespace AGX_Beat_Sync.UI;

/// <summary>
/// Computes panel bounds for the main editor layout.
/// Layout: Transport bar (top) | Track list (left) | Timeline (center) | Inspector (right).
/// Bottom row: Game View (left) | Event Console (right). Game view height is user-adjustable via the divider above it.
/// </summary>
public class PanelLayout
{
    public const int TransportHeight = 32;
    public const int TrackListWidth = 200;
    public const int InspectorWidth = 280;
    /// <summary>Fraction of the bottom row width for the game view (rest is Event Console). Used when GameViewWidthPx is 0.</summary>
    private const float GameViewWidthFraction = 0.45f;
    public const int MinGameViewWidth = 220;
    public const int MinEventConsoleWidth = 200;
    public const int DefaultGameViewHeight = 220;
    public const int MinGameViewHeight = 80;
    public const int MinTimelineHeight = 100;
    public const int StatusBarHeight = 26;

    /// <summary>Height of the game view panel in pixels. User can resize by dragging the divider.</summary>
    public int GameViewHeightPx { get; set; } = DefaultGameViewHeight;

    /// <summary>Width of the game view in the bottom row in pixels. 0 = use GameViewWidthFraction.</summary>
    public int GameViewWidthPx { get; set; }

    public Rectangle TransportBar { get; private set; }
    public Rectangle TrackList { get; private set; }
    public Rectangle Timeline { get; private set; }
    public Rectangle Inspector { get; private set; }
    public Rectangle EventConsole { get; private set; }
    public Rectangle GameView { get; private set; }
    public Rectangle StatusBar { get; private set; }

    /// <summary>Rectangle to hit-test for dragging the timeline/game view divider (top edge of game view).</summary>
    public Rectangle DividerGrip => new(GameView.X, GameView.Y - DividerGripHalfHeight, GameView.Width, DividerGripHalfHeight * 2);

    /// <summary>Rectangle to hit-test for dragging the game view / event console divider (vertical split).</summary>
    public Rectangle BottomRowDividerGrip => new(GameView.Right - BottomRowGripHalfWidth, GameView.Y, BottomRowGripHalfWidth * 2, GameView.Height);

    public const int DividerGripHalfHeight = 4;
    public const int BottomRowGripHalfWidth = 4;

    public void Update(int windowWidth, int windowHeight)
    {
        int w = windowWidth;
        int h = windowHeight;

        TransportBar = new Rectangle(0, 0, w, TransportHeight);

        int mainTop = TransportHeight;
        int mainHeight = h - TransportHeight - StatusBarHeight;
        Inspector = new Rectangle(w - InspectorWidth, mainTop, InspectorWidth, mainHeight);
        TrackList = new Rectangle(0, mainTop, TrackListWidth, mainHeight);

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
        int centerWidth = w - InspectorWidth - TrackListWidth;
        Timeline = new Rectangle(TrackListWidth, mainTop, centerWidth, centerHeight);

        StatusBar = new Rectangle(0, h - StatusBarHeight, w, StatusBarHeight);
    }
}
