namespace LocationSharer;

public sealed record LocationSnapshot(
    string? City,
    string? Country,
    string Source,
    DateTimeOffset TimestampUtc,
    double? Latitude = null,
    double? Longitude = null,
    double? AccuracyMeters = null
);
