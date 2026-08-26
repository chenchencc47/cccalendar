using CcCalendar.Core.Configuration;
using CcCalendar.Core.Weather;
using CcCalendar.Desktop.ViewModels;

namespace CcCalendar.Desktop.Tests.ViewModels;

public sealed class WeatherViewModelTests
{
    [Fact]
    public async Task RefreshFallsBackToSavedCityWhenAutomaticLocationFails()
    {
        WeatherLocation shanghai = CreateLocation("上海", 31.23, 121.47);
        var weatherService = new FakeWeatherService(shanghai);
        var settings = new WeatherLocationSettings
        {
            UseAutomaticLocation = true,
            ManualLocation = SavedWeatherLocation.From(shanghai),
        };
        var viewModel = new WeatherViewModel(
            weatherService,
            new FailingLocationProvider(),
            settings);

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal("上海 · 中国", viewModel.LocationName);
        Assert.Equal("31° · 多云", viewModel.CurrentSummary);
        Assert.Contains("自动定位失败", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectingSearchResultUsesAndPersistsManualCity()
    {
        WeatherLocation shanghai = CreateLocation("上海", 31.23, 121.47);
        WeatherLocation beijing = CreateLocation("北京", 39.90, 116.40);
        var weatherService = new FakeWeatherService(shanghai, beijing);
        WeatherLocationSettings? saved = null;
        var viewModel = new WeatherViewModel(
            weatherService,
            new FixedLocationProvider(shanghai),
            new WeatherLocationSettings(),
            updated => saved = updated);

        await viewModel.SearchAsync("北京", CancellationToken.None);

        Assert.True(viewModel.IsSearchResultsOpen);

        await viewModel.SelectManualLocationAsync(viewModel.SearchResults.Single(), CancellationToken.None);

        Assert.False(viewModel.IsAutomaticLocation);
        Assert.Equal("北京 · 中国", viewModel.LocationName);
        Assert.Equal("北京", saved!.ManualLocation!.Name);
        Assert.False(saved.UseAutomaticLocation);
        Assert.False(viewModel.IsSearchResultsOpen);
    }

    [Fact]
    public async Task CurrentLocationDisplaysCityRegionAndCountry()
    {
        var foshan = new WeatherLocation(
            "佛山",
            "广东",
            "中国",
            23.02,
            113.12,
            "Asia/Shanghai");
        var viewModel = new WeatherViewModel(
            new FakeWeatherService(foshan),
            new FixedLocationProvider(foshan),
            new WeatherLocationSettings());

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal("佛山 · 广东 · 中国", viewModel.LocationName);
    }

    [Fact]
    public async Task HourlyStartsAtCurrentHourAndIncludesCurrentConditions()
    {
        WeatherLocation foshan = CreateLocation("佛山", 23.02, 113.12);
        var observedAt = new DateTime(2026, 8, 17, 11, 36, 0);
        HourlyWeather[] sourceHours = [.. Enumerable.Range(0, 48).Select(hour =>
            new HourlyWeather(
                observedAt.Date.AddHours(hour),
                20 + hour,
                2,
                hour))];
        var forecast = new WeatherForecast(
            foshan,
            new CurrentWeather(observedAt, 33, 40, 3, 6),
            sourceHours,
            []);
        var viewModel = new WeatherViewModel(
            new FixedForecastWeatherService(forecast),
            new FixedLocationProvider(foshan),
            new WeatherLocationSettings());

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(26, viewModel.Hourly.Count);
        Assert.Equal(new DateTime(2026, 8, 17, 10, 0, 0), viewModel.Hourly[0].Time);
        Assert.Equal("10:00", viewModel.Hourly[0].TimeLabel);
        Assert.Equal(observedAt, viewModel.Hourly[1].Time);
        Assert.Equal("现在", viewModel.Hourly[1].TimeLabel);
        Assert.Equal("12:00", viewModel.Hourly[2].TimeLabel);
        Assert.Equal(new DateTime(2026, 8, 18, 11, 0, 0), viewModel.Hourly[^1].Time);
    }

    private static WeatherLocation CreateLocation(string name, double latitude, double longitude)
    {
        return new WeatherLocation(name, string.Empty, "中国", latitude, longitude, "Asia/Shanghai");
    }

    private sealed class FakeWeatherService(params WeatherLocation[] locations) : IWeatherService
    {
        public Task<IReadOnlyList<WeatherLocation>> SearchLocationsAsync(
            string query,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<WeatherLocation>>(
                locations.Where(location => location.Name.Contains(query, StringComparison.Ordinal)).ToArray());
        }

        public Task<WeatherForecast> GetForecastAsync(
            WeatherLocation location,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new WeatherForecast(
                location,
                new CurrentWeather(
                    new DateTime(2026, 8, 16, 14, 0, 0),
                    31,
                    34,
                    2,
                    10),
                [new HourlyWeather(new DateTime(2026, 8, 16, 14, 0, 0), 31, 2, 20)],
                [new DailyWeather(new DateOnly(2026, 8, 16), 2, 33, 27)]));
        }
    }

    private sealed class FailingLocationProvider : IAutomaticLocationProvider
    {
        public Task<WeatherLocation> GetCurrentLocationAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Location is unavailable.");
        }
    }

    private sealed class FixedForecastWeatherService(WeatherForecast forecast) : IWeatherService
    {
        public Task<IReadOnlyList<WeatherLocation>> SearchLocationsAsync(
            string query,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WeatherLocation>>([]);

        public Task<WeatherForecast> GetForecastAsync(
            WeatherLocation location,
            CancellationToken cancellationToken) =>
            Task.FromResult(forecast);
    }

    private sealed class FixedLocationProvider(WeatherLocation location) : IAutomaticLocationProvider
    {
        public Task<WeatherLocation> GetCurrentLocationAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(location);
        }
    }
}
