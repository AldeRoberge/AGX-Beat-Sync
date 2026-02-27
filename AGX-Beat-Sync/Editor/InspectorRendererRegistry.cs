using AGX_Beat_Sync.Core;

namespace AGX_Beat_Sync.Editor;

/// <summary>
/// Maps event track type ids to their inspector renderers. Register renderers here so the InspectorPanel can draw them.
/// </summary>
public static class InspectorRendererRegistry
{
    private static readonly Dictionary<string, IInspectorRenderer> s_renderers = new();

    public static void Register(string trackTypeId, IInspectorRenderer renderer)
    {
        s_renderers[trackTypeId] = renderer;
    }

    public static IInspectorRenderer? Get(string trackTypeId)
    {
        return s_renderers.TryGetValue(trackTypeId, out var r) ? r : null;
    }
}
