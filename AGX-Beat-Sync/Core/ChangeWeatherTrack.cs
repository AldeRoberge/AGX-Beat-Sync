namespace AGX_Beat_Sync.Core;

/// <summary>Weather options for the Change Weather event (game view scene).</summary>
public enum WeatherKind
{
    Sunny,
    Rain,
}

/// <summary>
/// Event track that changes the weather in the game view when events fire.
/// Each event can set the weather to Rain or Sunny.
/// </summary>
public class ChangeWeatherTrack : EventTrackBase
{
    public override string TrackTypeId => "ChangeWeather";

    public ChangeWeatherTrack()
    {
        DisplayName = "Change Weather";
    }

    /// <summary>Per-event weather. Missing key = use default (Sunny).</summary>
    public Dictionary<double, WeatherKind> EventWeathers { get; set; } = new();

    /// <summary>Weather for the event at the given time. Returns default when not set.</summary>
    public WeatherKind GetWeather(double eventTime)
    {
        return EventWeathers.TryGetValue(eventTime, out var w) ? w : WeatherKind.Sunny;
    }

    /// <summary>Set weather for an event at the given time.</summary>
    public void SetWeather(double eventTime, WeatherKind kind)
    {
        EventWeathers[eventTime] = kind;
    }
}
