using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.Model;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;

public class FuturesTickDataStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IDbContextFactory dbFactory,
    IActorService actorService,
    ILogger<FuturesTickDataStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FuturesTickDataCommandState>
{
    /// <summary>
    /// load futures tick data state from snapshot event
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask<FuturesTickDataCommandState> LoadStateAsync(ICommand command)
        => await LoadStateFromSnapshotAsync<FuturesTickDataCommandState, FuturesTickDataStreamingStartedEvent>(command);

    /// <summary>
    /// save futures tick data state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesTickDataCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the futures tick data state
    /// asynchronously.
    /// </summary>
    /// <remarks>This method processes each domain event in the provided collection and either posts the event
    /// or updates the read model accordingly. Streaming events are posted directly, while insert events
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
                FuturesTickDataStreamingStartedEvent e => await PostEventAsync<FuturesTickDataStreamingStartedEvent, FuturesTickDataStreamingId>(context, e),
                FuturesTickDataStreamingStoppedEvent e => await PostEventAsync<FuturesTickDataStreamingStoppedEvent, FuturesTickDataStreamingId>(context, e),
                FuturesTickDataInsertedEvent e => await UpdateReadModelAsync<FuturesTickDataInsertedEvent, FuturesTickDataInsertedCompleteEvent, FuturesTickDataInsertedFailEvent, FuturesTickDataId>(
                    context, e, async () => await InsertFuturesTickDataAsync(db, e.TickData)),
                _ => false
            };
        }

        static async ValueTask InsertFuturesTickDataAsync(IMarketDataDbContext db, FuturesTickDataV2ReadModel futuresTickData)
            => await db.InsertFuturesTickDataAsync(futuresTickData);
    }
    
}
