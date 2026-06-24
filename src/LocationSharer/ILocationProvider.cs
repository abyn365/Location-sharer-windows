namespace LocationSharer;

public interface ILocationProvider
{
    Task<LocationSnapshot?> TryGetAsync(CancellationToken cancellationToken);
}
