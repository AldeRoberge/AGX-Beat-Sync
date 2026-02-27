using Microsoft.Xna.Framework;

namespace AGX_Beat_Sync.UI;

/// <summary>
/// Computes panel bounds for the main editor layout.
/// Layout: Transport bar (top) | Timeline (center-left) | Inspector (right) | Game View (bottom).
/// When GameViewExpanded is true, Game View takes the full main area over the timeline.
/// </summary>
public class PanelLayout
{
    public const int TransportHeight = 32;
    public const int InspectorWidth = 280;
    public const int GameViewHeight = 220;

    /// <summary>When true, game view occupies the timeline area (timeline is hidden).</summary>
    public bool GameViewExpanded { get; set; }

    public Rectangle TransportBar { get; private set; }
    public Rectangle Timeline { get; private set; }
    public Rectangle Inspector { get; private set; }
    public Rectangle GameView { get; private set; }

    public void Update(int windowWidth, int windowHeight)
    {
        int w = windowWidth;
        int h = windowHeight;

        TransportBar = new Rectangle(0, 0, w, TransportHeight);

        int mainTop = TransportHeight;
        int mainHeight = h - TransportHeight;
        int centerWidth = w - InspectorWidth;
        Inspector = new Rectangle(w - InspectorWidth, mainTop, InspectorWidth, mainHeight);

        if (GameViewExpanded)
        {
            GameView = new Rectangle(0, mainTop, centerWidth, mainHeight);
            Timeline = new Rectangle(0, mainTop, centerWidth, 0);
        }
        else
        {
            int bottomPanelHeight = Math.Min(GameViewHeight, mainHeight / 3);
            int centerHeight = mainHeight - bottomPanelHeight;
            GameView = new Rectangle(0, h - bottomPanelHeight, w, bottomPanelHeight);
            Timeline = new Rectangle(0, mainTop, centerWidth, centerHeight);
        }
    }
}
