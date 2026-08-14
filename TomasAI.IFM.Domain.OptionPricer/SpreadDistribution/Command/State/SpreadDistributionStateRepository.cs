using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.Actor;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.State;

/// <summary>
/// Provides functionality to manage the state of spread distributions, including loading state from snapshots and saving
/// state changes. This repository is designed to work with event-sourced actors.
/// </summary>
/// <remarks>This class extends <see cref="BaseEventSourceActorRepository"/> and implements <see
/// cref="IEventSourceActorStateRepository{SpreadDistributionCommandState}"/> to provide specialized behavior for managing <see
/// cref="SpreadDistributionCommandState"/> entities. It relies on an event-sourcing pattern to persist and retrieve
/// state.</remarks>
/// <param name="aggregateFactory"></param>
/// <param name="dbEventSource"></param>
/// <param name="dbFactory"></param>
/// <param name="actorService"></param>
/// <param name="logger"></param>
public class SpreadDistributionStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IDbContextFactory dbFactory,
    IActorService actorService,
    IEventProjector<SpreadDistributionCommandActor> eventProjector,
    ILogger<SpreadDistributionStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<SpreadDistributionCommandState>
{
    /// <summary>
    /// load spread distribution state from snapshot event
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask<SpreadDistributionCommandState> LoadStateAsync(ICommand command)
        => await LoadStateAsync(command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask<SpreadDistributionCommandState> LoadStateAsync(ICommand command, CancellationToken cancellationToken)
        => await LoadStateFromSnapshotLastNRangeAsync<SpreadDistributionCommandState, SpreadDistributionInsertedEvent, SpreadDistributionDeletedEvent>(command, 0, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// save spread distribution state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, SpreadDistributionCommandState state, ICommand command)
       => await SaveStateAsync(context, state, command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask SaveStateAsync(ICommandActorContext context, SpreadDistributionCommandState state, ICommand command, CancellationToken cancellationToken)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the spread distribution query state
    /// asynchronously.
    /// </summary>
    /// <remarks>This method processes each domain event in the provided collection and posts the corresponding
    /// events. It is typically called as part of the event sourcing workflow to keep the read model in sync with the
    /// latest events.</remarks>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
        => eventProjector.DomainEventsProjectionAsync(domainEvents);
}
