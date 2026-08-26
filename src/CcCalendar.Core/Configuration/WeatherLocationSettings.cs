using CcCalendar.Core.Weather;

namespace CcCalendar.Core.Configuration;

public sealed record SavedWeatherLocation
{
    public string Name { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public string TimeZoneId { get; init; } = "UTC";

    public static SavedWeatherLocation From(WeatherLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return new SavedWeatherLocation
        {
            Name = location.Name,
            Region = location.Region,
            Country = location.Country,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            TimeZoneId = location.TimeZoneId,
        };
    }

    public WeatherLocation ToWeatherLocation()
    {
        return new WeatherLocation(Name, Region, Country, Latitude, Longitude, TimeZoneId);
    }
}

public sealed record WeatherLocationSettings
{
    public bool UseAutomaticLocation { get; init; } = true;

    public SavedWeatherLocation? ManualLocation { get; init; }
}
