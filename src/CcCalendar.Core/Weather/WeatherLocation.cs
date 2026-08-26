namespace CcCalendar.Core.Weather;

public sealed record WeatherLocation(
    string Name,
    string Region,
    string Country,
    double Latitude,
    double Longitude,
    string TimeZoneId)
{
    public string DisplayName => string.Join(
        " · ",
        new[] { Name, Region, Country }.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct());
}
