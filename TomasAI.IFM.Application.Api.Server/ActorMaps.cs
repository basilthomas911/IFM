using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

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
    static IActorSupervisor _supervisor = default!;
    static string _serviceId = "ActorMaps";
    static List<IActor> _actors = [];

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
    /// <returns>The same web application instance, enabling method chaining.</returns>
    public static WebApplication MapEventModelActors(this WebApplication app, ILogger logger)
    {
        // get supervisor...
        _supervisor = app.Services.GetRequiredService<IActorSupervisor>();

        // get actor types...
        var registry = _supervisor.Container.Resolve<IActorRegistry>();

        // get actor factory...
        var factory = _supervisor.Container.Resolve<IActorFactory>();

        // add all domain actors to supervisor...
        var actorTypes = registry.ActorTypes;
        foreach (var actorType in actorTypes)
        {
            var actor = factory.GetActor(actorType);
            _actors.Add(actor);
            _supervisor.AddActor(actor);
            var producer = _supervisor.Container.Resolve<IActorProducer>();
            if (producer is not null)
                _supervisor.AddProducer(actor.Id, producer);

            if (actor.Id.ActorType == ActorType.Event)
            {
                var jsProducer = _supervisor.Container.Resolve<IJSActorProducer>();
                if (jsProducer is not null)
                    _supervisor.AddJSProducer(actor.Id, jsProducer);
            }

        }

        var actorService = _supervisor.Container.Resolve<IActorService>();
        List<ActorType> consumerTypes = [ActorType.Command, ActorType.Query, ActorType.Supervisor];
        foreach (var consumerType in consumerTypes)
        {
            var consumer = _supervisor.Container.Resolve<IActorConsumer>();
            if (consumer is not null)
                _supervisor.AddConsumer(consumerType, consumer);
        }

        List<ActorType> jsConsumerTypes = [ActorType.Event];
        foreach (var consumerType in jsConsumerTypes)
        {
            var consumer = _supervisor.Container.Resolve<IJSActorConsumer>();
            if (consumer is not null)
                _supervisor.AddConsumer(consumerType, consumer);
        }

        Task.Run(async () =>
        {
            try
            {
                await _supervisor.StartConsumersAsync();
                foreach (var e in _actors)
                {
                    await e.StartAsync(_supervisor);
                    logger.LogInformationEvent(_serviceId, "Started {ActorType} actor.", e.GetType().Name);
                }
                logger.LogInformationEvent(_serviceId, "Event model actor supervisor started with {ActorCount} actors.", actorTypes.Length);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start event model actor supervisor.");
            }
        }).Wait();

        return app;
    }
}
