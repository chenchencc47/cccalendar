using System.Globalization;
using CcCalendar.Core.Weather;
using Windows.Devices.Geolocation;

namespace CcCalendar.Desktop.Weather;

public sealed class WindowsGeolocationProvider : IAutomaticLocationProvider
{
    public async Task<WeatherLocation> GetCurrentLocationAsync(CancellationToken cancellationToken)
    {
        GeolocationAccessStatus access = await Geolocator.RequestAccessAsync();
        if (access != GeolocationAccessStatus.Allowed)
        {
            throw new InvalidOperationException("Windows location access is not allowed.");
        }

        var geolocator = new Geolocator
        {
            DesiredAccuracy = PositionAccuracy.Default,
            MovementThreshold = 500,
        };
        Geoposition position = await geolocator.GetGeopositionAsync().AsTask(cancellationToken);
        BasicGeoposition coordinate = position.Coordinate.Point.Position;
        CivicAddress address = position.CivicAddress;
        string city = address.City ?? string.Empty;
        string region = address.State ?? string.Empty;
        string country = address.Country switch
        {
            "CN" or "CHN" => "中国",
            string value => value,
        };
        string name = string.IsNullOrWhiteSpace(city)
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{coordinate.Latitude:F4}, {coordinate.Longitude:F4}")
            : city;
        return new WeatherLocation(
            name,
            region,
            country,
            coordinate.Latitude,
            coordinate.Longitude,
            "UTC");
    }
}
