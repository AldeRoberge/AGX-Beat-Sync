using Microsoft.Xna.Framework;

namespace AGX_Beat_Sync.UI;

/// <summary>
/// Computes panel bounds for the main editor layout.
/// Layout: Transport bar (top) | Timeline (center-left) | Inspector (right) | Game View (bottom).
/// Game view height is user-adjustable by dragging the divider above it.
/// </summary>
public class PanelLayout
{
    public const int TransportHeight = 32;
    public const int InspectorWidth = 280;
    public const int DefaultGameViewHeight = 220;
    public const int MinGameViewHeight = 80;
    public const int MinTimelineHeight = 100;

    /// <summary>Height of the game view panel in pixels. User can resize by dragging the divider.</summary>
    public int GameViewHeightPx { get; set; } = DefaultGameViewHeight;

    public Rectangle TransportBar { get; private set; }
    public Rectangle Timeline { get; private set; }
    public Rectangle Inspector { get; private set; }
    public Rectangle GameView { get; private set; }

    /// <summary>Rectangle to hit-test for dragging the timeline/game view divider (top edge of game view).</summary>
    public Rectangle DividerGrip => new(GameView.X, GameView.Y - DividerGripHalfHeight, GameView.Width, DividerGripHalfHeight * 2);

    public const int DividerGripHalfHeight = 4;

    public void Update(int windowWidth, int windowHeight)
    {
        int w = windowWidth;
        int h = windowHeight;

        TransportBar = new Rectangle(0, 0, w, TransportHeight);

        int mainTop = TransportHeight;
        int mainHeight = h - TransportHeight;
        int centerWidth = w - InspectorWidth;
        Inspector = new Rectangle(w - InspectorWidth, mainTop, InspectorWidth, mainHeight);

        int maxGameViewHeight = Math.Max(MinGameViewHeight, mainHeight - MinTimelineHeight);
        int bottomPanelHeight = Math.Clamp(GameViewHeightPx, MinGameViewHeight, maxGameViewHeight);
        GameViewHeightPx = bottomPanelHeight;

        int centerHeight = mainHeight - bottomPanelHeight;
        GameView = new Rectangle(0, h - bottomPanelHeight, w, bottomPanelHeight);
        Timeline = new Rectangle(0, mainTop, centerWidth, centerHeight);
    }
}
