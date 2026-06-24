using System.Text.Json;
using Windows.Devices.Geolocation;

namespace LocationSharer;

public sealed class WindowsLocationProvider : ILocationProvider
{
    private static readonly HttpClient Http = CreateHttp();

    public async Task<LocationSnapshot?> TryGetAsync(CancellationToken cancellationToken)
    {
        var access = await Geolocator.RequestAccessAsync();
        if (access != GeolocationAccessStatus.Allowed)
        {
            return null;
        }

        var geolocator = new Geolocator
        {
            DesiredAccuracyInMeters = 50
        };

        Geoposition position;
        try
        {
            position = await geolocator.GetGeopositionAsync();
        }
        catch
        {
            return null;
        }

        var point = position.Coordinate.Point?.Position;
        if (point is null)
        {
            return null;
        }

        string? city = null;
        string? country = null;

        try
        {
            (city, country) = await ReverseGeocodeAsync(point.Value.Latitude, point.Value.Longitude, cancellationToken);
        }
        catch
        {
            // Keep coordinates even if reverse geocoding fails.
        }

        return new LocationSnapshot(
            City: city,
            Country: country,
            Source: "windows_location",
            TimestampUtc: DateTimeOffset.UtcNow,
            Latitude: point.Value.Latitude,
            Longitude: point.Value.Longitude,
            AccuracyMeters: position.Coordinate.Accuracy);
    }

    private static async Task<(string? City, string? Country)> ReverseGeocodeAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var url = $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("LocationSharer/1.0");

        using var response = await Http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!doc.RootElement.TryGetProperty("address", out var address))
        {
            return (null, null);
        }

        string? city =
            GetString(address, "city") ??
            GetString(address, "town") ??
            GetString(address, "village") ??
            GetString(address, "municipality") ??
            GetString(address, "county");

        string? country = GetString(address, "country");
        return (city, country);
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? value.GetString() : null;

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        return http;
    }
}
