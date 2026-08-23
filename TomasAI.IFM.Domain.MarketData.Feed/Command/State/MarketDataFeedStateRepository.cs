using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.State;

/// <summary>
/// Provides functionality to manage the state of market data feeds, including loading state from snapshots and saving
/// state changes. This repository is designed to work with event-sourced actors.
/// </summary>
/// <remarks>This class extends <see cref="BaseEventSourceActorRepository"/> and implements <see
/// cref="IEventSourceActorStateRepository{MarketDataFeedCommandState}"/> to provide specialized behavior for managing <see
/// cref="MarketDataFeedCommandState"/> entities. It relies on an event-sourcing pattern to persist and retrieve
/// state.</remarks>
/// <param name="aggregateFactory"></param>
/// <param name="dbEventSource"></param>
/// <param name="actorService"></param>
/// <param name="logger"></param>
public class MarketDataFeedStateRepository(
    ICommandActorContext<MarketDataFeedCommandActor> actorContext)
    : BaseEventSourceActorRepository(
        actorContext.StateFactory,
        actorContext.DbEventSource,
        actorContext.ActorService,
        actorContext.Logger),
      IEventSourceActorStateRepository<MarketDataFeedCommandState>
{
    readonly IEventProjector<MarketDataFeedCommandActor> _eventProjector =
        IsArgumentNull.Set(actorContext.EventProjector);
    /// <summary>
    /// load market data feed state from snapshot event
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask<MarketDataFeedCommandState> LoadStateAsync(ICommand command)
        => command switch
        {
            _ when command is TurnTradeLiveFeedOnCommand => await LoadStateFromSnapshotAsync<MarketDataFeedCommandState, TradeLiveFeedAddedEvent>(command),
            _ when command is TurnTradeLiveFeedOffCommand => await LoadStateFromSnapshotAsync<MarketDataFeedCommandState, TradeLiveFeedAddedEvent>(command),
            _ when command is AddTradeLiveFeedCommand => await LoadStateFromSnapshotAsync<MarketDataFeedCommandState, TradeLiveFeedAddedEvent>(command),
            _ when command is RemoveTradeLiveFeedCommand => await LoadStateFromSnapshotAsync<MarketDataFeedCommandState, TradeLiveFeedAddedEvent>(command),
            _ when command is HaltTradeLiveFeedCommand => await LoadStateFromSnapshotAsync<MarketDataFeedCommandState, TradeLiveFeedAddedEvent>(command),
            _ => await LoadStateFromSnapshotAsync<MarketDataFeedCommandState, MarketDataFeedStartedEvent>(command)
        };

    /// <summary>
    /// save market data feed state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, MarketDataFeedCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the market data feed query state
    /// asynchronously.
    /// </summary>
    /// <remarks>This method processes each domain event in the provided collection and posts the corresponding
    /// events. It is typically called as part of the event sourcing workflow to keep the read model in sync with the
    /// latest events.</remarks>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
        => await _eventProjector.DomainEventsProjectionAsync(domainEvents).ConfigureAwait(false);
}
