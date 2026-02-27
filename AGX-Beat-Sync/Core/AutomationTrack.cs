namespace AGX_Beat_Sync.Core;

public class AutomationTrack
{
    public string Name { get; set; } = "Automation";
    public AutomationTarget Target { get; set; }
    public List<AutomationKeyframe> Keyframes { get; set; } = new();
}
