namespace CcCalendar.Core.Weather;

public interface IWeatherService
{
    Task<IReadOnlyList<WeatherLocation>> SearchLocationsAsync(
        string query,
        CancellationToken cancellationToken);

    Task<WeatherForecast> GetForecastAsync(
        WeatherLocation location,
        CancellationToken cancellationToken);
}
