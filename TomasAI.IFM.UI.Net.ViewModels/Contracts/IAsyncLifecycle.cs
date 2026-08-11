namespace TomasAI.IFM.UI.Net.Contracts;

/// <summary>
/// Defines cooperative initialization and shutdown for presentation components
/// that own listeners, timers, channels, or other asynchronous resources.
/// </summary>
public interface IAsyncLifecycle
{
    /// <summary>
    /// Initializes the component and its owned asynchronous resources.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops accepting work and awaits all owned asynchronous resources.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}
