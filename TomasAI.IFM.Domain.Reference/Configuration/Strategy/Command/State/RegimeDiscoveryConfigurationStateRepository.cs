using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.State;

/// <summary>Persists strategy-configuration lifecycle events in the PostgreSQL event log.</summary>
public sealed class RegimeDiscoveryConfigurationStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<RegimeDiscoveryConfigurationCommandActor> projector,
    ILogger<RegimeDiscoveryConfigurationStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<RegimeDiscoveryConfigurationCommandState>
{
    /// <summary>Loads the complete configuration stream.</summary>
    public async ValueTask<RegimeDiscoveryConfigurationCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync<RegimeDiscoveryConfigurationCommandState>(command, CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>Saves pending lifecycle events atomically.</summary>
    public async ValueTask SaveStateAsync(ICommandActorContext context,
        RegimeDiscoveryConfigurationCommandState state, ICommand command)
        => await SaveStateAndDenormalizeEventsAsync(context, state, command, CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>Queues committed lifecycle events for ConfigurationDb projection.</summary>
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
        => projector.DomainEventsProjectionAsync(events);
}
