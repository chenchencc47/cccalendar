namespace CcCalendar.Core.Weather;

public sealed record CurrentWeather(
    DateTime ObservedAt,
    double TemperatureCelsius,
    double ApparentTemperatureCelsius,
    int WeatherCode,
    double WindSpeedKilometersPerHour);

public sealed record HourlyWeather(
    DateTime Time,
    double TemperatureCelsius,
    int WeatherCode,
    int PrecipitationProbabilityPercent);

public sealed record DailyWeather(
    DateOnly Date,
    int WeatherCode,
    double MaximumTemperatureCelsius,
    double MinimumTemperatureCelsius);

public sealed record WeatherForecast(
    WeatherLocation Location,
    CurrentWeather Current,
    IReadOnlyList<HourlyWeather> Hourly,
    IReadOnlyList<DailyWeather> Daily);
