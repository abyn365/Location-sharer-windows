namespace LocationSharer;

public sealed class LocationService : IDisposable
{
    private readonly ILocationProvider[] _providers =
    [
        new WindowsLocationProvider(),
        new IpGeolocationProvider()
    ];

    private readonly LocationSharingClient _client = new();
    private readonly AppSettings _settings;
    private readonly Action<string> _statusChanged;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public bool IsRunning => _cts is not null;

    public LocationService(AppSettings settings, Action<string> statusChanged)
    {
        _settings = settings;
        _statusChanged = statusChanged;
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings.EndpointUrl = settings.EndpointUrl;
        _settings.Secret = settings.Secret;
        _settings.IntervalMinutes = settings.IntervalMinutes;
        _settings.ConsentAccepted = settings.ConsentAccepted;
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        _cts!.Cancel();
        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts.Dispose();
        _cts = null;
        _loopTask = null;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await SendOnceAsync(cancellationToken).ConfigureAwait(false);

            var delay = TimeSpan.FromMinutes(Math.Clamp(_settings.IntervalMinutes, 1, 1440));
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task SendOnceAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.EndpointUrl))
        {
            _statusChanged("No endpoint configured.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.Secret))
        {
            _statusChanged("No shared secret configured.");
            return;
        }

        _statusChanged("Reading location...");

        LocationSnapshot? snapshot = null;
        foreach (var provider in _providers)
        {
            snapshot = await provider.TryGetAsync(cancellationToken).ConfigureAwait(false);
            if (snapshot is not null)
            {
                break;
            }
        }

        if (snapshot is null)
        {
            _statusChanged("No location could be resolved.");
            return;
        }

        try
        {
            _statusChanged($"Sending {snapshot.Source} update...");
            await _client.SendAsync(_settings.EndpointUrl, _settings.Secret, snapshot, cancellationToken).ConfigureAwait(false);
            _settings.LastSentAtUtc = DateTimeOffset.UtcNow;
            SettingsStore.Save(_settings);
            _statusChanged($"Last sent: {_settings.LastSentAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        }
        catch (Exception ex)
        {
            _statusChanged($"Send failed: {ex.Message}");
        }
    }
}
