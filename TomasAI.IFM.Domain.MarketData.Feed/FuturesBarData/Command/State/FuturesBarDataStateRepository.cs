using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;

public class FuturesBarDataStateRepository(
    ICommandActorContext<FuturesBarDataCommandActor> actorContext)
    : BaseEventSourceActorRepository(
        actorContext.StateFactory,
        actorContext.DbEventSource,
        actorContext.ActorService,
        actorContext.Logger),
      IEventSourceActorStateRepository<FuturesBarDataCommandState>
{
    readonly IEventProjector<FuturesBarDataCommandActor> _eventProjector =
        IsArgumentNull.Set(actorContext.EventProjector);
    /// <summary>
    /// load futures bar data state from snapshot event
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask<FuturesBarDataCommandState> LoadStateAsync(ICommand command)
        => await LoadStateFromSnapshotLastNRangeAsync<
            FuturesBarDataCommandState,
            FuturesBarDataStreamingStartedEvent,
            FuturesBarDataInsertedEvent>(command, 0);

    /// <summary>
    /// save futures bar data state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesBarDataCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the futures bar data query state
    /// asynchronously.
    /// </summary>
    /// <remarks>This method processes each domain event in the provided collection and either posts the event
    /// or updates the read model accordingly. Streaming events are posted directly, while insert and delete events
    /// update the read model via <see cref="IMarketDataDbContext"/>.</remarks>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
        => await _eventProjector.DomainEventsProjectionAsync(domainEvents).ConfigureAwait(false);
}
