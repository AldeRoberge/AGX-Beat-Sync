using AGX_Beat_Sync.Core;
using AGX_Beat_Sync.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.Editor;

/// <summary>
/// Renders the inspector UI for a specific event track type. Each track type registers its own renderer.
/// </summary>
public interface IInspectorRenderer
{
    /// <summary>Draw the inspector for the given track. Advances cursorY as rows are drawn. selection is the current editor selection (e.g. for per-event properties).</summary>
    void Draw(SpriteBatch spriteBatch, Rectangle contentArea, IEventTrack track, InputManager input, ref int cursorY, EditorSelection? selection);

    /// <summary>Handle input (clicks, typing) for the inspector. Called when this panel has focus / mouse is in content area.</summary>
    void Update(IEventTrack track, InputManager input, Rectangle contentArea, EditorSelection? selection);
}
