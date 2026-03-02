namespace AGX_Beat_Sync.Core;

/// <summary>
/// Event track that shows a chat bubble above the enemy with custom text when events fire.
/// Each event can have its own text; missing key = empty string.
/// </summary>
public class DialogueTrack : EventTrackBase
{
    public override string TrackTypeId => "Dialogue";

    public DialogueTrack()
    {
        DisplayName = "Dialogue";
    }

    /// <summary>Per-event dialogue text. Missing key = empty.</summary>
    public Dictionary<double, string> EventTexts { get; set; } = new();

    /// <summary>Text for the event at the given time. Returns empty when not set.</summary>
    public string GetText(double eventTime)
    {
        return EventTexts.TryGetValue(eventTime, out var s) ? s : "";
    }

    /// <summary>Set text for an event at the given time.</summary>
    public void SetText(double eventTime, string text)
    {
        if (string.IsNullOrEmpty(text))
            EventTexts.Remove(eventTime);
        else
            EventTexts[eventTime] = text;
    }
}
