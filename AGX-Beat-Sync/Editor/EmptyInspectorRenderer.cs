using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.Editor;

public class EmptyInspectorRenderer : IInspectorRenderer
{
    public void Draw(SpriteBatch sb, Rectangle contentArea, IEventTrack track, InputManager input, ref int cursorY, EditorSelection? selection)
    {
        // Empty track has no properties; track type is shown at top of inspector panel
    }

    public void Update(IEventTrack track, InputManager input, Rectangle contentArea, EditorSelection? selection)
    {
    }
}
