namespace LocationSharer;

public sealed record LocationPayload(
    string? City,
    string? Country,
    string Secret,
    string Source,
    DateTimeOffset TimestampUtc,
    double? Latitude = null,
    double? Longitude = null,
    double? AccuracyMeters = null
);
