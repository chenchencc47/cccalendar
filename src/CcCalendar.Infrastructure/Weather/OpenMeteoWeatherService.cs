using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CcCalendar.Core.Weather;

namespace CcCalendar.Infrastructure.Weather;

public sealed class OpenMeteoWeatherService(HttpClient httpClient) : IWeatherService
{
    public async Task<IReadOnlyList<WeatherLocation>> SearchLocationsAsync(
        string query,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        string trimmedQuery = query.Trim();
        string normalizedQuery = NormalizeLocationName(trimmedQuery);
        GeocodingResult[] results = await SearchGeocodingAsync(
            trimmedQuery,
            cancellationToken);
        if (ShouldTryCitySuffix(trimmedQuery)
            && !results.Any(result =>
                string.Equals(
                    NormalizeLocationName(result.Name),
                    normalizedQuery,
                    StringComparison.OrdinalIgnoreCase)
                && GetFeatureRank(result.FeatureCode) >= 3))
        {
            GeocodingResult[] cityResults = await SearchGeocodingAsync(
                trimmedQuery + "市",
                cancellationToken);
            results = [.. results
                .Concat(cityResults)
                .DistinctBy(result => (result.Latitude, result.Longitude))];
        }

        return results
            .Where(result => GetFeatureRank(result.FeatureCode) >= 3)
            .OrderByDescending(result => string.Equals(
                NormalizeLocationName(result.Name),
                normalizedQuery,
                StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(result => GetFeatureRank(result.FeatureCode))
            .ThenByDescending(result => result.Population ?? 0)
            .Take(10)
            .Select(result => new WeatherLocation(
                NormalizeChineseCityName(result.Name, result.Country),
                NormalizeChineseRegionName(result.Region, result.Country),
                NormalizeCountryName(result.Country),
                result.Latitude,
                result.Longitude,
                result.TimeZoneId ?? "UTC"))
            .ToArray();
    }

    public async Task<WeatherForecast> GetForecastAsync(
        WeatherLocation location,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(location);
        WeatherLocation resolvedLocation = await ResolveCoordinateLocationAsync(
            location,
            cancellationToken);
        string latitude = location.Latitude.ToString(CultureInfo.InvariantCulture);
        string longitude = location.Longitude.ToString(CultureInfo.InvariantCulture);
        string url = "https://api.open-meteo.com/v1/forecast"
            + $"?latitude={latitude}&longitude={longitude}"
            + "&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m"
            + "&hourly=temperature_2m,weather_code,precipitation_probability"
            + "&daily=weather_code,temperature_2m_max,temperature_2m_min"
            + "&forecast_days=7&timezone=auto";
        ForecastResponse response = await httpClient.GetFromJsonAsync<ForecastResponse>(
            url,
            cancellationToken)
            ?? throw new InvalidDataException("The weather service returned no forecast.");

        CurrentResponse current = response.Current
            ?? throw new InvalidDataException("The weather forecast has no current conditions.");
        HourlyResponse hourly = response.Hourly
            ?? throw new InvalidDataException("The weather forecast has no hourly data.");
        DailyResponse daily = response.Daily
            ?? throw new InvalidDataException("The weather forecast has no daily data.");
        resolvedLocation = resolvedLocation with
        {
            TimeZoneId = response.TimeZoneId ?? resolvedLocation.TimeZoneId,
        };

        return new WeatherForecast(
            resolvedLocation,
            new CurrentWeather(
                ParseDateTime(current.Time),
                current.Temperature,
                current.ApparentTemperature,
                current.WeatherCode,
                current.WindSpeed),
            MapHourly(hourly),
            MapDaily(daily));
    }

    private async Task<WeatherLocation> ResolveCoordinateLocationAsync(
        WeatherLocation location,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(location.Region) || !location.Name.Contains(','))
        {
            return location;
        }

        string latitude = location.Latitude.ToString(CultureInfo.InvariantCulture);
        string longitude = location.Longitude.ToString(CultureInfo.InvariantCulture);
        string url = "https://api.bigdatacloud.net/data/reverse-geocode-client"
            + $"?latitude={latitude}&longitude={longitude}&localityLanguage=zh";
        try
        {
            ReverseGeocodingResponse? response =
                await httpClient.GetFromJsonAsync<ReverseGeocodingResponse>(url, cancellationToken);
            string? name = response?.City ?? response?.Locality;
            if (string.IsNullOrWhiteSpace(name))
            {
                return location;
            }

            string country = response!.CountryCode is "CN" or "CHN"
                ? "中国"
                : response.CountryName ?? location.Country;
            return location with
            {
                Name = name,
                Region = response.PrincipalSubdivision ?? string.Empty,
                Country = country,
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return location;
        }
    }

    private static HourlyWeather[] MapHourly(HourlyResponse hourly)
    {
        int count = new[]
        {
            hourly.Times.Length,
            hourly.Temperatures.Length,
            hourly.WeatherCodes.Length,
            hourly.PrecipitationProbabilities.Length,
        }.Min();
        return Enumerable.Range(0, count)
            .Select(index => new HourlyWeather(
                ParseDateTime(hourly.Times[index]),
                hourly.Temperatures[index],
                hourly.WeatherCodes[index],
                hourly.PrecipitationProbabilities[index]))
            .ToArray();
    }

    private static DailyWeather[] MapDaily(DailyResponse daily)
    {
        int count = new[]
        {
            daily.Dates.Length,
            daily.WeatherCodes.Length,
            daily.MaximumTemperatures.Length,
            daily.MinimumTemperatures.Length,
        }.Min();
        return Enumerable.Range(0, count)
            .Select(index => new DailyWeather(
                DateOnly.Parse(daily.Dates[index], CultureInfo.InvariantCulture),
                daily.WeatherCodes[index],
                daily.MaximumTemperatures[index],
                daily.MinimumTemperatures[index]))
            .ToArray();
    }

    private static DateTime ParseDateTime(string value)
    {
        return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.None);
    }

    private async Task<GeocodingResult[]> SearchGeocodingAsync(
        string query,
        CancellationToken cancellationToken)
    {
        string url = "https://geocoding-api.open-meteo.com/v1/search"
            + $"?name={Uri.EscapeDataString(query)}&count=100&language=zh&format=json";
        GeocodingResponse? response = await httpClient.GetFromJsonAsync<GeocodingResponse>(
            url,
            cancellationToken);
        return response?.Results ?? [];
    }

    private static string NormalizeLocationName(string value) =>
        value.Trim().TrimEnd('市');

    private static string NormalizeChineseCityName(string value, string? country)
    {
        if (!IsChina(country)
            || value.EndsWith('市')
            || value.EndsWith('州')
            || value.EndsWith('盟')
            || value.EndsWith("地区", StringComparison.Ordinal))
        {
            return value;
        }

        return value + "市";
    }

    private static string NormalizeChineseRegionName(string? value, string? country)
    {
        if (string.IsNullOrWhiteSpace(value) || !IsChina(country))
        {
            return value ?? string.Empty;
        }

        return value switch
        {
            "北京" => "北京市",
            "上海" => "上海市",
            "天津" => "天津市",
            "重庆" => "重庆市",
            "内蒙古" => "内蒙古自治区",
            "广西" => "广西壮族自治区",
            "西藏" => "西藏自治区",
            "宁夏" => "宁夏回族自治区",
            "新疆" => "新疆维吾尔自治区",
            "香港" => "香港特别行政区",
            "澳门" => "澳门特别行政区",
            _ when value.EndsWith('省')
                || value.EndsWith('市')
                || value.EndsWith("自治区", StringComparison.Ordinal)
                || value.EndsWith("特别行政区", StringComparison.Ordinal) => value,
            _ => value + "省",
        };
    }

    private static string NormalizeCountryName(string? country) =>
        IsChina(country) ? "中国" : country ?? string.Empty;

    private static bool IsChina(string? country) =>
        country is "中国" or "中华人民共和国" or "CN" or "CHN" or "China";

    private static bool ShouldTryCitySuffix(string value)
    {
        return value.Length is >= 2 and <= 4
            && value.All(character => character is >= '\u3400' and <= '\u9FFF')
            && value[^1] is not ('市' or '区' or '县' or '州' or '盟');
    }

    private static int GetFeatureRank(string? featureCode)
    {
        return featureCode?.ToUpperInvariant() switch
        {
            "PPLC" => 7,
            "PPLA" => 6,
            "PPLA1" => 5,
            "PPLA2" => 4,
            "PPLA3" => 3,
            "PPLA4" => 2,
            "PPL" => 1,
            _ => 0,
        };
    }

    private sealed record GeocodingResponse(
        [property: JsonPropertyName("results")] GeocodingResult[]? Results);

    private sealed record GeocodingResult(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("admin1")] string? Region,
        [property: JsonPropertyName("country")] string? Country,
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude,
        [property: JsonPropertyName("timezone")] string? TimeZoneId,
        [property: JsonPropertyName("feature_code")] string? FeatureCode,
        [property: JsonPropertyName("population")] long? Population);

    private sealed record ReverseGeocodingResponse(
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("locality")] string? Locality,
        [property: JsonPropertyName("principalSubdivision")] string? PrincipalSubdivision,
        [property: JsonPropertyName("countryName")] string? CountryName,
        [property: JsonPropertyName("countryCode")] string? CountryCode);

    private sealed record ForecastResponse(
        [property: JsonPropertyName("timezone")] string? TimeZoneId,
        [property: JsonPropertyName("current")] CurrentResponse? Current,
        [property: JsonPropertyName("hourly")] HourlyResponse? Hourly,
        [property: JsonPropertyName("daily")] DailyResponse? Daily);

    private sealed record CurrentResponse(
        [property: JsonPropertyName("time")] string Time,
        [property: JsonPropertyName("temperature_2m")] double Temperature,
        [property: JsonPropertyName("apparent_temperature")] double ApparentTemperature,
        [property: JsonPropertyName("weather_code")] int WeatherCode,
        [property: JsonPropertyName("wind_speed_10m")] double WindSpeed);

    private sealed record HourlyResponse(
        [property: JsonPropertyName("time")] string[] Times,
        [property: JsonPropertyName("temperature_2m")] double[] Temperatures,
        [property: JsonPropertyName("weather_code")] int[] WeatherCodes,
        [property: JsonPropertyName("precipitation_probability")] int[] PrecipitationProbabilities);

    private sealed record DailyResponse(
        [property: JsonPropertyName("time")] string[] Dates,
        [property: JsonPropertyName("weather_code")] int[] WeatherCodes,
        [property: JsonPropertyName("temperature_2m_max")] double[] MaximumTemperatures,
        [property: JsonPropertyName("temperature_2m_min")] double[] MinimumTemperatures);
}
