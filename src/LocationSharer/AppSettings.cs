namespace LocationSharer;

public sealed class AppSettings
{
    public string EndpointUrl { get; set; } = "https://your-domain.com/api/location";
    public string Secret { get; set; } = string.Empty;
    public int IntervalMinutes { get; set; } = 5;
    public bool ConsentAccepted { get; set; }
    public DateTimeOffset? LastSentAtUtc { get; set; }
}
