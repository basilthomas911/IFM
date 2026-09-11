using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;

/// <summary>Persists strategy-configuration lifecycle events in the PostgreSQL event log.</summary>
public sealed class ParameterStartupStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<ParameterStartupCommandActor> projector,
    ILogger<ParameterStartupStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<ParameterStartupCommandState>
{
    /// <summary>Loads the complete configuration stream.</summary>
    public async ValueTask<ParameterStartupCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync<ParameterStartupCommandState>(command, CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>Saves pending lifecycle events atomically.</summary>
    public async ValueTask SaveStateAsync(ICommandActorContext context,
        ParameterStartupCommandState state, ICommand command)
        => await SaveStateAndDenormalizeEventsAsync(context, state, command, CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>Queues committed lifecycle events for ConfigurationDb projection.</summary>
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection events)
        => projector.DomainEventsProjectionAsync(events);
}
