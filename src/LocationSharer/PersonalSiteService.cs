using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocationSharer;

public sealed class PersonalSiteService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly AppSettings _settings;
    private CancellationTokenSource? _cts;
    private Task? _pollTask;

    public event Action<VisitorStats?>? VisitorStatsUpdated;
    public event Action<DiscordStatus?>? DiscordStatusUpdated;
    public event Action<string>? StatusMessageUpdated;

    public bool IsPolling => _cts is not null;

    public PersonalSiteService(AppSettings settings)
    {
        _settings = settings;
    }

    public void Start()
    {
        if (IsPolling) return;

        _cts = new CancellationTokenSource();
        _pollTask = Task.Run(() => PollingLoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (!IsPolling) return;

        _cts!.Cancel();
        if (_pollTask is not null)
        {
            try
            {
                await _pollTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                StatusMessageUpdated?.Invoke($"Polling stop error: {ex.Message}");
            }
        }

        _cts.Dispose();
        _cts = null;
        _pollTask = null;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _http.Dispose();
    }

    private async Task PollingLoopAsync(CancellationToken cancellationToken)
    {
        int iteration = 0;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            string? baseUrl = GetBaseUrl();
            if (string.IsNullOrEmpty(baseUrl))
            {
                StatusMessageUpdated?.Invoke("Site API URL not configured.");
            }
            else
            {
                // Discord status is polled every 20 seconds (every iteration)
                _ = FetchDiscordStatusAsync(baseUrl, cancellationToken);

                // Visitor stats is polled every 120 seconds (every 6th iteration, or on iteration 0)
                if (iteration % 6 == 0)
                {
                    _ = FetchVisitorStatsAsync(baseUrl, cancellationToken);
                }
            }

            iteration = (iteration + 1) % 6;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private string? GetBaseUrl()
    {
        if (string.IsNullOrWhiteSpace(_settings.EndpointUrl)) return null;
        try
        {
            var uri = new Uri(_settings.EndpointUrl);
            return $"{uri.Scheme}://{uri.Authority}";
        }
        catch
        {
            return null;
        }
    }

    private async Task FetchDiscordStatusAsync(string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            string url = $"{baseUrl.TrimEnd('/')}/api/discord-status";
            var status = await _http.GetFromJsonAsync<DiscordStatus>(url, JsonOptions, cancellationToken).ConfigureAwait(false);
            DiscordStatusUpdated?.Invoke(status);
        }
        catch (Exception ex)
        {
            // Fail silently or report error
            StatusMessageUpdated?.Invoke($"Discord API failed: {ex.Message}");
            DiscordStatusUpdated?.Invoke(null);
        }
    }

    private async Task FetchVisitorStatsAsync(string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            string url = $"{baseUrl.TrimEnd('/')}/api/visitor-stats";
            var stats = await _http.GetFromJsonAsync<VisitorStats>(url, JsonOptions, cancellationToken).ConfigureAwait(false);
            VisitorStatsUpdated?.Invoke(stats);
        }
        catch (Exception ex)
        {
            StatusMessageUpdated?.Invoke($"Stats API failed: {ex.Message}");
            VisitorStatsUpdated?.Invoke(null);
        }
    }
}
