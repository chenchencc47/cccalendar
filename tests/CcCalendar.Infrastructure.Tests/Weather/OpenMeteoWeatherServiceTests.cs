using System.Net;
using System.Text;
using CcCalendar.Infrastructure.Weather;

namespace CcCalendar.Infrastructure.Tests.Weather;

public sealed class OpenMeteoWeatherServiceTests
{
    [Fact]
    public async Task SearchKeepsCityLevelResultsAndNormalizesChineseAdministrativeNames()
    {
        var handler = new QueueHttpMessageHandler(
            """
            {"results":[
              {"name":"佛山","admin1":"云南","country":"中国","latitude":24.1,"longitude":102.1,"timezone":"Asia/Shanghai","feature_code":"PPLA4","population":1200},
              {"name":"佛山","admin1":"重庆市","country":"中国","latitude":30.0,"longitude":106.4,"timezone":"Asia/Shanghai","feature_code":"PPL","population":800}
            ]}
            """,
            """
            {"results":[
              {"name":"佛山市","admin1":"广东","country":"中国","latitude":23.02,"longitude":113.12,"timezone":"Asia/Shanghai","feature_code":"PPLA2","population":9042509}
            ]}
            """);
        var service = new OpenMeteoWeatherService(new HttpClient(handler));

        IReadOnlyList<CcCalendar.Core.Weather.WeatherLocation> locations =
            await service.SearchLocationsAsync("佛山", CancellationToken.None);

        CcCalendar.Core.Weather.WeatherLocation location = Assert.Single(locations);
        Assert.Equal("佛山市", location.Name);
        Assert.Equal("广东省", location.Region);
        Assert.Equal("佛山市 · 广东省 · 中国", location.DisplayName);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("count=100", handler.Requests[0].RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("佛山市"), handler.Requests[1].RequestUri!.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAndForecastMapOpenMeteoResponses()
    {
        var handler = new QueueHttpMessageHandler(
            """
            {"results":[{"name":"上海","admin1":"上海","country":"中国","latitude":31.23,"longitude":121.47,"timezone":"Asia/Shanghai","feature_code":"PPLA","population":24874500}]}
            """,
            """
            {
              "timezone":"Asia/Shanghai",
              "current":{"time":"2026-08-16T14:00","temperature_2m":31.2,"apparent_temperature":35.1,"weather_code":2,"wind_speed_10m":12.4},
              "hourly":{"time":["2026-08-16T14:00","2026-08-16T15:00"],"temperature_2m":[31.2,30.8],"weather_code":[2,61],"precipitation_probability":[20,70]},
              "daily":{"time":["2026-08-16","2026-08-17"],"weather_code":[2,61],"temperature_2m_max":[33.0,31.0],"temperature_2m_min":[27.0,26.0]}
            }
            """);
        var service = new OpenMeteoWeatherService(new HttpClient(handler));

        var locations = await service.SearchLocationsAsync("上海", CancellationToken.None);
        var forecast = await service.GetForecastAsync(locations.Single(), CancellationToken.None);

        Assert.Equal("上海市", forecast.Location.Name);
        Assert.Equal("上海市 · 中国", forecast.Location.DisplayName);
        Assert.Equal(31.2, forecast.Current.TemperatureCelsius);
        Assert.Equal(35.1, forecast.Current.ApparentTemperatureCelsius);
        Assert.Equal(2, forecast.Current.WeatherCode);
        Assert.Equal(2, forecast.Hourly.Count);
        Assert.Equal(70, forecast.Hourly[1].PrecipitationProbabilityPercent);
        Assert.Equal(2, forecast.Daily.Count);
        Assert.Equal(new DateOnly(2026, 8, 17), forecast.Daily[1].Date);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.Contains("forecast", handler.Requests[1].RequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForecastResolvesCoordinateFallbackToCity()
    {
        var handler = new QueueHttpMessageHandler(
            """
            {"city":"佛山市","locality":"禅城区","principalSubdivision":"广东省","countryName":"中华人民共和国","countryCode":"CN"}
            """,
            """
            {
              "timezone":"Asia/Shanghai",
              "current":{"time":"2026-08-17T14:00","temperature_2m":33.0,"apparent_temperature":40.0,"weather_code":3,"wind_speed_10m":6.0},
              "hourly":{"time":[],"temperature_2m":[],"weather_code":[],"precipitation_probability":[]},
              "daily":{"time":[],"weather_code":[],"temperature_2m_max":[],"temperature_2m_min":[]}
            }
            """);
        var service = new OpenMeteoWeatherService(new HttpClient(handler));
        var coordinates = new CcCalendar.Core.Weather.WeatherLocation(
            "23.0250, 113.1460",
            string.Empty,
            "CN",
            23.025,
            113.146,
            "UTC");

        var forecast = await service.GetForecastAsync(coordinates, CancellationToken.None);

        Assert.Equal("佛山市 · 广东省 · 中国", forecast.Location.DisplayName);
        Assert.Contains("reverse-geocode-client", handler.Requests[0].RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("forecast", handler.Requests[1].RequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    private sealed class QueueHttpMessageHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> responses = new(responses);

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses.Dequeue(), Encoding.UTF8, "application/json"),
            });
        }
    }
}
