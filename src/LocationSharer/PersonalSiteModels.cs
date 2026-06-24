using System.Text.Json.Serialization;

namespace LocationSharer;

public sealed record VisitorStats(
    [property: JsonPropertyName("active")] int Active,
    [property: JsonPropertyName("pageviews")] int Pageviews,
    [property: JsonPropertyName("uniques")] int Uniques
);

public sealed record DiscordTimestamps(
    [property: JsonPropertyName("start")] long? Start,
    [property: JsonPropertyName("end")] long? End
);

public sealed record DiscordActivity(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("details")] string? Details,
    [property: JsonPropertyName("state")] string? State,
    [property: JsonPropertyName("image")] string? Image,
    [property: JsonPropertyName("smallImage")] string? SmallImage,
    [property: JsonPropertyName("largeText")] string? LargeText,
    [property: JsonPropertyName("smallText")] string? SmallText,
    [property: JsonPropertyName("timestamps")] DiscordTimestamps? Timestamps
);

public sealed record SpotifyTrack(
    [property: JsonPropertyName("album")] string Album,
    [property: JsonPropertyName("albumArtUrl")] string AlbumArtUrl,
    [property: JsonPropertyName("artist")] string Artist,
    [property: JsonPropertyName("song")] string Song,
    [property: JsonPropertyName("trackId")] string TrackId,
    [property: JsonPropertyName("songUrl")] string SongUrl,
    [property: JsonPropertyName("timestamps")] DiscordTimestamps? Timestamps
);

public sealed record DiscordStatus(
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("activeDevice")] string? ActiveDevice,
    [property: JsonPropertyName("isOnline")] bool IsOnline,
    [property: JsonPropertyName("activity")] DiscordActivity? Activity,
    [property: JsonPropertyName("spotify")] SpotifyTrack? Spotify
);
