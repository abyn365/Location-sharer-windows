using System.Text.Json;

namespace LocationSharer;

public sealed class IpGeolocationProvider : ILocationProvider
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task<LocationSnapshot?> TryGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Http.GetAsync("https://ipapi.co/json/", cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            string? city = GetString(doc.RootElement, "city");
            string? country = GetString(doc.RootElement, "country_name") ?? GetString(doc.RootElement, "country");

            if (string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(country))
            {
                return null;
            }

            return new LocationSnapshot(
                City: city,
                Country: country,
                Source: "ip_geolocation",
                TimestampUtc: DateTimeOffset.UtcNow);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? value.GetString() : null;
}
