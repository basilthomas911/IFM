using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.State;

/// <summary>
/// Provides functionality to manage the state of futures option quote data, including loading state from snapshots
/// and saving state changes. This repository is designed to work with event-sourced actors.
/// </summary>
/// <remarks>This class extends <see cref="BaseEventSourceActorRepository"/> and implements <see
/// cref="IEventSourceActorStateRepository{FuturesOptionQuoteDataCommandState}"/> to provide specialized behavior for managing <see
/// cref="FuturesOptionQuoteDataCommandState"/> entities. It relies on an event-sourcing pattern to persist and retrieve
/// state.</remarks>
/// <param name="aggregateFactory"></param>
/// <param name="dbEventSource"></param>
/// <param name="actorService"></param>
/// <param name="dbFactory"></param>
/// <param name="logger"></param>
public class FuturesOptionQuoteDataStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IDbContextFactory dbFactory,
    IBlackboardService blackboardService,
    ILogger<FuturesOptionQuoteDataStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FuturesOptionQuoteDataCommandState>
{
    /// <summary>
    /// Asynchronously loads the state associated with the specified command.
    /// </summary>
    /// <remarks>This method initializes and retrieves an empty state for the given command. Use this method
    /// when a fresh state is required for command processing.</remarks>
    /// <param name="command">The command for which to load the state. This parameter determines which command's state is initialized.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the loaded state as a <see
    /// cref="FuturesOptionQuoteDataCommandState"/> instance.</returns>
    public async ValueTask<FuturesOptionQuoteDataCommandState> LoadStateAsync(ICommand command)
        => await LoadEmptyStateAsync<FuturesOptionQuoteDataCommandState>();

    /// <summary>
    /// save futures option quote data state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesOptionQuoteDataCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="domainEvents"></param>
    /// <returns></returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
    {
        var db = dbFactory.MarketDataDb;
        foreach (var domainEvent in domainEvents)
        {
            _ = domainEvent switch
            {
                FuturesOptionQuoteDataStreamingStartedEvent e => await UpdateReadModelAsync<FuturesOptionQuoteDataStreamingStartedEvent, FuturesOptionQuoteDataStreamingStartedCompleteEvent, FuturesOptionQuoteDataStreamingStartedFailEvent, QuoteId>(
                    context, e, async () => await InsertFuturesOptionQuoteAsync(db, e.FuturesOptionQuotes, e.FuturesOptionQuoteData)),
                FuturesOptionQuoteDataStreamingStoppedEvent e => await UpdateReadModelAsync<FuturesOptionQuoteDataStreamingStoppedEvent, FuturesOptionQuoteDataStreamingStoppedCompleteEvent, FuturesOptionQuoteDataStreamingStoppedFailEvent, QuoteId>(
                    context, e, async () => await DeleteFuturesOptionQuotesAsync(db, e.QuoteId)),
                FuturesOptionQuoteDataInsertedEvent e => await UpdateReadModelAsync<FuturesOptionQuoteDataInsertedEvent, FuturesOptionQuoteDataInsertedCompleteEvent, FuturesOptionQuoteDataInsertedFailEvent, QuoteId>(
                    context, e, async () => await InsertFuturesOptionQuoteDataAsync(db, e.OptionQuoteData)),
                _ => false
            };
        }

        async Task InsertFuturesOptionQuoteAsync(IMarketDataDbContext db, ICollection<FuturesOptionQuoteReadModel> quotes, ICollection<FuturesOptionQuoteDataReadModel> quoteData)
        {
            await db.InsertFuturesOptionQuoteAsync(quotes, quoteData);
            foreach (var e in quoteData)
            {
                FuturesOptionQuoteId optionQuoteId = new(e.QuoteId, e.ContractId, e.RequestId);
                blackboardService.FuturesOptionQuoteData.Set(optionQuoteId, e);
            }
        }

        static async ValueTask DeleteFuturesOptionQuotesAsync(IMarketDataDbContext db, int quoteId)
            => await db.DeleteFuturesOptionQuotesAsync(quoteId);

        static async ValueTask InsertFuturesOptionQuoteDataAsync(IMarketDataDbContext db, FuturesOptionQuoteDataReadModel optionQuoteData)
            => await db.InsertFuturesOptionQuoteDataAsync(optionQuoteData);
    }

}
