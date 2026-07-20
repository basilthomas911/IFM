using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;

public class FuturesBarDataStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IDbContextFactory dbFactory,
    ILogger<FuturesBarDataStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FuturesBarDataCommandState>
{
    /// <summary>
    /// load futures bar data state from snapshot event
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask<FuturesBarDataCommandState> LoadStateAsync(ICommand command)
        => await LoadStateFromSnapshotAsync<FuturesBarDataCommandState, FuturesBarDataStreamingStartedEvent>(command);

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
    {
        var db = dbFactory.MarketDataDb;
        foreach (var domainEvent in domainEvents)
        {
            _ = domainEvent switch
            {
                FuturesBarDataStreamingStartedEvent e => await PostEventAsync<FuturesBarDataStreamingStartedEvent, FuturesBarDataStreamingId>(context, e),
                FuturesBarDataStreamingStoppedEvent e => await PostEventAsync<FuturesBarDataStreamingStoppedEvent, FuturesBarDataStreamingId>(context, e),
                FuturesBarDataInsertedEvent e => await UpdateReadModelAsync<FuturesBarDataInsertedEvent, FuturesBarDataInsertedCompleteEvent, FuturesBarDataInsertedFailEvent, FuturesBarDataId>(
                    context, e, async () => await InsertFuturesBarDataAsync(db, e.FuturesBarData)),
                FuturesBarDataDeletedEvent e => await UpdateReadModelAsync<FuturesBarDataDeletedEvent, FuturesBarDataDeletedCompleteEvent, FuturesBarDataDeletedFailEvent, FuturesBarDataId>(
                    context, e, async () => await DeleteFuturesBarDataAsync(db, e.BarDataId)),
                _ => false
            };
        }

        static async ValueTask InsertFuturesBarDataAsync(IMarketDataDbContext db, FuturesBarDataReadModel futuresBarData)
            => await db.InsertFuturesBarDataAsync(futuresBarData);

        static async ValueTask DeleteFuturesBarDataAsync(IMarketDataDbContext db, FuturesBarDataId futuresBarDataId)
            => await db.DeleteFuturesBarDataAsync(futuresBarDataId);
    }
}
