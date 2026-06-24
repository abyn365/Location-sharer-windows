using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace LocationSharer;

public sealed class LocationSharingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public async Task SendAsync(
        string endpointUrl,
        string secret,
        LocationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var payload = new LocationPayload(
            City: snapshot.City,
            Country: snapshot.Country,
            Secret: secret,
            Source: snapshot.Source,
            TimestampUtc: snapshot.TimestampUtc,
            Latitude: snapshot.Latitude,
            Longitude: snapshot.Longitude,
            AccuracyMeters: snapshot.AccuracyMeters
        );

        var json = JsonSerializer.Serialize(payload, JsonOptions);

#if DEBUG
        MessageBox.Show(
            json,
            "LocationSharer - Payload Debug",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
#endif

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var response = await _http.PostAsync(
            endpointUrl,
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new HttpRequestException(
                $"Server returned {(int)response.StatusCode} {response.StatusCode}\n\n{body}");
        }
    }
}