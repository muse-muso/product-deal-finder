namespace ProductDealFinder.Infrastructure.Relay;

/// <summary>
/// Listens for HTTP POSTs from the Firefox extension and triggers email alerts via the desktop app's SMTP.
/// </summary>
public interface IExtensionRelayService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Stops and starts the listener (e.g. after user changes port or enabled in settings).
    /// </summary>
    Task RestartAsync(CancellationToken cancellationToken = default);
}
