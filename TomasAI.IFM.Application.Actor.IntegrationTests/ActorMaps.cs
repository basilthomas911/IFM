using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

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
    public static IActorSupervisor Supervisor => _supervisor;

    static IActorSupervisor _supervisor = default!;
    static string _serviceId = "ActorMaps";
    static List<IActor> _actors = [];
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
