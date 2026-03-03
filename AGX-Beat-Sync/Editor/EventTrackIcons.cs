using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AGX_Beat_Sync.Editor;

/// <summary>
/// Icons for event track types, loaded from the Content/Icons folder (e.g. Empty.png, SpawnEntity.png).
/// Textures are registered by the game after loading; draw calls look up by track type id and scale to the requested size.
/// </summary>
public static class EventTrackIcons
{
    public const int DefaultSize = 16;

    private static readonly Dictionary<(GraphicsDevice, string), Texture2D> s_textures = new();

    /// <summary>Registers an icon texture for a track type. Called by the game when loading content.</summary>
    public static void RegisterIcon(GraphicsDevice device, string trackTypeId, Texture2D texture)
    {
        var key = (device, trackTypeId);
        if (s_textures.TryGetValue(key, out var existing) && existing != texture)
            existing.Dispose();
        s_textures[key] = texture;
    }

    /// <summary>Draws the icon for the given track type at (x, y) with optional size and tint. Uses DefaultSize (16) if size is 0. Does nothing if no texture was registered for that type.</summary>
    public static void DrawIcon(SpriteBatch sb, GraphicsDevice device, int x, int y, string trackTypeId, int size = 0, Color? tint = null)
    {
        if (size <= 0) size = DefaultSize;
        var tex = GetTexture(device, trackTypeId);
        if (tex == null || tex.IsDisposed) return;
        var color = tint ?? Color.White;
        var dest = new Rectangle(x, y, size, size);
        var src = new Rectangle(0, 0, tex.Width, tex.Height);
        sb.Draw(tex, dest, src, color);
    }

    /// <summary>Returns the registered icon texture for the track type, or null if none.</summary>
    public static Texture2D? GetTexture(GraphicsDevice device, string trackTypeId)
    {
        var key = (device, trackTypeId);
        return s_textures.TryGetValue(key, out var tex) ? tex : null;
    }

    /// <summary>Clears all registered icons and disposes their textures (e.g. on device reset).</summary>
    public static void ClearCache()
    {
        foreach (var (_, tex) in s_textures)
            tex?.Dispose();
        s_textures.Clear();
    }
}
