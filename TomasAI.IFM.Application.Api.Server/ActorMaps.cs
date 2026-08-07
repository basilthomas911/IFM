using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// Provides extension methods for configuring and registering event model actors within a web application pipeline.
/// </summary>
/// <remarks>The ActorMaps class enables integration of domain actors, producers, and consumers into the
/// application's dependency injection and event processing infrastructure. Use its methods during application startup
/// to ensure actors are properly registered and started. This class is intended for use with applications employing an
/// actor-based event model architecture.</remarks>
public static class ActorMaps
{
    /// <summary>
    /// Configures and registers event model actors, producers, and consumers with the application's actor supervisor,
    /// and starts all actors and consumers required for event-driven processing.
    /// </summary>
    /// <remarks>This method resolves required actor services from the application's dependency injection
    /// container, adds all domain actors to the supervisor, and starts both actors and consumers asynchronously. It
    /// logs the status of each actor as it is started and reports errors encountered during initialization. Call this
    /// method during application startup to ensure all event model actors are properly configured and
    /// running.</remarks>
    /// <param name="app">The web application instance to which the event model actors and related services will be registered.</param>
    /// <param name="logger">The logger used to record informational and error messages during actor initialization and startup.</param>
    /// <param name="cancellationToken">Cancels actor infrastructure startup before the host begins accepting requests.</param>
    /// <returns>The same web application instance, enabling method chaining.</returns>
    public static async Task<WebApplication> MapEventModelActorsAsync(
        this WebApplication app,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var supervisor = app.Services.GetRequiredService<IActorSupervisor>();
        await ActorRuntimeStartup
            .StartAsync(supervisor, logger, cancellationToken)
            .ConfigureAwait(false);

        return app;
    }
}
