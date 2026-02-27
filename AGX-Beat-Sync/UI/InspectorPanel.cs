using AGX_Beat_Sync.Editor;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.UI;

public class InspectorPanel : PanelBase
{
    public EditorSelection? Selection { get; set; }

    public InspectorPanel()
    {
        Title = "Inspector";
        BackgroundColor = new Color(38, 40, 44);
    }

    protected override void DrawContent(SpriteBatch spriteBatch)
    {
        if (Selection?.SelectedNote == null)
            return;

        var pixel = GetPixelTexture(spriteBatch.GraphicsDevice);
        var content = ContentBounds;
        int x = content.X + 12;
        int y = content.Y + 12;
        int w = content.Width - 24;
        int row = 22;

        // Header: "Note"
        spriteBatch.Draw(pixel, new Rectangle(x, y, w, 18), new Color(55, 58, 65));
        y += row;

        // Time / Lane / Type as simple colored bars (placeholder until we have font)
        spriteBatch.Draw(pixel, new Rectangle(x, y, w, 16), new Color(70, 74, 82));
        y += row;
        spriteBatch.Draw(pixel, new Rectangle(x, y, w, 16), new Color(70, 74, 82));
        y += row;
        spriteBatch.Draw(pixel, new Rectangle(x, y, w, 16), new Color(70, 74, 82));
    }
}
