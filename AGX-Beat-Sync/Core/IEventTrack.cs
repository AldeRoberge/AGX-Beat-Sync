using Microsoft.Xna.Framework;

namespace AGX_Beat_Sync.Core;

/// <summary>
/// Base for modular event tracks (e.g. SpawnEntity, PlaySound). Each track type defines its own data and inspector UI.
/// </summary>
public interface IEventTrack
{
    /// <summary>Unique id for this track type (e.g. "SpawnEntity"). Used by registry and persistence.</summary>
    string TrackTypeId { get; }

    /// <summary>Display name shown in the track list and inspector header.</summary>
    string DisplayName { get; set; }

    /// <summary>Order index for reordering in the track list.</summary>
    int Order { get; set; }
}
