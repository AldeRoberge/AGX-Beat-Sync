namespace AGX_Beat_Sync.Core;

public class AutomationKeyframe
{
    public double Time { get; set; }
    public float Value { get; set; }
    public InterpolationMode Mode { get; set; } = InterpolationMode.Linear;
}
