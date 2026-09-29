using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

namespace TomasAI.IFM.Application.Actor.IntegrationTests;

/// <summary>
/// Provides methods and properties for registering and managing event model actors within a web application, enabling
/// centralized actor supervision and event processing.
/// </summary>
/// <remarks>Call the MapEventModelActors method during application startup to ensure that all required actors,
/// producers, and consumers are registered and started. This class maintains a reference to the actor supervisor and
/// manages the lifecycle of domain actors. Exceptions that occur during actor startup are logged using the provided
/// logger. This class is intended to be used as part of the application's initialization pipeline.</remarks>
public static class ActorMaps
{
    public static async Task<WebApplication> MapEventModelActorsAsync(
        this WebApplication app,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var bootstrap = app.Services.GetRequiredService<ISupervisorBootstrap>();
        var bootstrapResult = await bootstrap.StartSupervisorAsync(cancellationToken).ConfigureAwait(false);
        if (!bootstrapResult.Succeeded)
            throw new InvalidOperationException(
                $"Supervisor bootstrap failed at {bootstrapResult.Stage}: {bootstrapResult.FailureReason}");

        var lifecycle = app.Services.GetRequiredService<ISupervisorManagedActorLifecycle>();
        var managedResult = await lifecycle.StartupActorsAsync(cancellationToken).ConfigureAwait(false);
        if (!managedResult.Succeeded)
        {
            await bootstrap.StopSupervisorAsync(CancellationToken.None).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Managed actor startup failed at {managedResult.Stage}: {managedResult.FailureReason}");
        }

        return app;
    }
}
