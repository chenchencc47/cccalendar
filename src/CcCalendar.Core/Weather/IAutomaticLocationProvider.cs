namespace CcCalendar.Core.Weather;

public interface IAutomaticLocationProvider
{
    Task<WeatherLocation> GetCurrentLocationAsync(CancellationToken cancellationToken);
}
