using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Model;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.State;

/// <summary>
/// Provides functionality to manage the state of futures EOD data, including loading state and saving
/// state changes. This repository is designed to work with event-sourced actors.
/// </summary>
/// <remarks>This class extends <see cref="BaseEventSourceActorRepository"/> and implements <see
/// cref="IEventSourceActorStateRepository{FuturesEodDataCommandState}"/> to provide specialized behavior for managing <see
/// cref="FuturesEodDataCommandState"/> entities. It relies on an event-sourcing pattern to persist and retrieve
/// state.</remarks>
/// <param name="aggregateFactory"></param>
/// <param name="dbEventSource"></param>
/// <param name="actorService"></param>
/// <param name="dbFactory"></param>
/// <param name="logger"></param>
public class FuturesEodDataStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IDbContextFactory dbFactory,
    ILogger<FuturesEodDataStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FuturesEodDataCommandState>
{
    /// <summary>
    /// load futures eod data state
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask<FuturesEodDataCommandState> LoadStateAsync(ICommand command)
        => await LoadEmptyStateAsync<FuturesEodDataCommandState>();

    /// <summary>
    /// save futures eod data state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesEodDataCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the futures EOD data state
    /// asynchronously.
    /// </summary>
    /// <remarks>This method processes each domain event in the provided collection and updates the read model
    /// accordingly. Insert events update the read model via <see cref="IMarketDataDbContext"/>.</remarks>
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
                FuturesEodDataInsertedEvent e => await UpdateReadModelAsync<FuturesEodDataInsertedEvent, FuturesEodDataInsertedCompleteEvent, FuturesEodDataInsertedFailEvent, FuturesEodDataId>(
                    context, e, async () => await InsertFuturesEodDataAsync(db, e.FuturesEodData)),
                VixFuturesEodDataInsertedEvent e => await UpdateReadModelAsync<VixFuturesEodDataInsertedEvent, VixFuturesEodDataInsertedCompleteEvent, VixFuturesEodDataInsertedFailEvent, FuturesEodDataId>(
                    context, e, async () => await InsertVixFuturesEodDataAsync(db, e.VixFuturesTickData)),
                _ => false
            };
        }

        static async ValueTask InsertFuturesEodDataAsync(IMarketDataDbContext db, FuturesEodDataV2ReadModel futuresEodData)
            => await db.InsertFuturesEodDataAsync(futuresEodData);

        static async ValueTask InsertVixFuturesEodDataAsync(IMarketDataDbContext db, FuturesTickDataV2ReadModel futuresTickData)
            => await db.InsertVixFuturesEodDataAsync(futuresTickData);
    }
}
