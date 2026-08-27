using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.State;

/// <summary>Loads and saves authoritative Regime Discovery state through the PostgreSQL event log.</summary>
public sealed class RegimeDiscoveryStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext eventSource,
    IActorService actorService,
    IEventProjector<RegimeDiscoveryCommandActor> eventProjector,
    ILogger<RegimeDiscoveryStateRepository> logger)
    : BaseEventSourceActorRepository(stateFactory, eventSource, actorService, logger),
      IEventSourceActorStateRepository<RegimeDiscoveryCommandState>
{
    readonly IEventProjector<RegimeDiscoveryCommandActor> _eventProjector =
        eventProjector ?? throw new ArgumentNullException(nameof(eventProjector));

    /// <summary>Reconstructs the complete private state for the command entity stream.</summary>
    public ValueTask<RegimeDiscoveryCommandState> LoadStateAsync(ICommand command)
        => LoadStateAsync(command, CancellationToken.None);

    /// <summary>Reconstructs the complete private state for the command entity stream.</summary>
    public async ValueTask<RegimeDiscoveryCommandState> LoadStateAsync(
        ICommand command,
        CancellationToken cancellationToken)
        => await LoadStateAsync<RegimeDiscoveryCommandState>(command, cancellationToken).ConfigureAwait(false);

    /// <summary>Persists pending events as one ACID event-log transaction.</summary>
    public ValueTask SaveStateAsync(
        ICommandActorContext context,
        RegimeDiscoveryCommandState state,
        ICommand command)
        => SaveStateAsync(context, state, command, CancellationToken.None);

    /// <summary>Persists pending events as one ACID event-log transaction.</summary>
    public async ValueTask SaveStateAsync(
        ICommandActorContext context,
        RegimeDiscoveryCommandState state,
        ICommand command,
        CancellationToken cancellationToken)
        => await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Queues committed private events for non-durable projection after the ACID commit.</summary>
    protected override ValueTask DenormalizeEventsAsync(
        ICommandActorContext context,
        DomainEventCollection domainEvents)
        => _eventProjector.DomainEventsProjectionAsync(domainEvents);
}
