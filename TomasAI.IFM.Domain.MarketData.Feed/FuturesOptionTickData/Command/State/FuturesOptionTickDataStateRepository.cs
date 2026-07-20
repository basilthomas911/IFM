using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Model;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;

/// <summary>
/// Provides functionality to manage the state of futures option tick data, including loading state and saving
/// state changes. This repository is designed to work with event-sourced actors.
/// </summary>
/// <remarks>This class extends <see cref="BaseEventSourceActorRepository"/> and implements <see
/// cref="IEventSourceActorStateRepository{FuturesOptionTickDataCommandState}"/> to provide specialized behavior for managing <see
/// cref="FuturesOptionTickDataCommandState"/> entities. It relies on an event-sourcing pattern to persist and retrieve
/// state.</remarks>
/// <param name="aggregateFactory"></param>
/// <param name="dbEventSource"></param>
/// <param name="actorService"></param>
/// <param name="logger"></param>
public class FuturesOptionTickDataStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IDbContextFactory dbFactory,
    ISequenceIdGenerator sequenceIdGenerator,
    IActorService actorService,
    ILogger<FuturesOptionTickDataStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FuturesOptionTickDataCommandState>
{
    /// <summary>
    /// Asynchronously loads the state associated with the specified command.
    /// </summary>
    /// <remarks>This method initializes and retrieves an empty state for the given command. Use this method
    /// when a fresh state is required for command processing.</remarks>
    /// <param name="command">The command for which to load the state. This parameter determines which command's state is initialized.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the loaded state as a <see
    /// cref="FuturesOptionTickDataCommandState"/> instance.</returns>
    public async ValueTask<FuturesOptionTickDataCommandState> LoadStateAsync(ICommand command)
        => await LoadEmptyStateAsync<FuturesOptionTickDataCommandState>();

    /// <summary>
    /// save futures option tick data state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesOptionTickDataCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the futures option tick data state
    /// asynchronously.
    /// </summary>
    /// <remarks>This method processes each domain event in the provided collection and posts the corresponding
    /// events. It is typically called as part of the event sourcing workflow to keep the read model in sync with the
    /// latest events.</remarks>
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
                FuturesOptionTickDataStreamingStartedEvent e => await PostEventAsync<FuturesOptionTickDataStreamingStartedEvent, FuturesOptionTickEntityId>(context, e),
                FuturesOptionTickDataStreamingStoppedEvent e => await PostEventAsync<FuturesOptionTickDataStreamingStoppedEvent, FuturesOptionTickEntityId>(context, e),
                FuturesOptionTickDataInsertedEvent e => await UpdateReadModelAsync<FuturesOptionTickDataInsertedEvent, FuturesOptionTickDataInsertedCompleteEvent, FuturesOptionTickDataInsertedFailEvent, FuturesOptionTickEntityId>(
                    context, e, async () => await InsertFuturesOptionTickDataAsync(db, e.TickData)),
                _ => false
            };
        }

        static async ValueTask InsertFuturesOptionTickDataAsync(IMarketDataDbContext db, FuturesOptionTickDataV2ReadModel optionTickData)
            => await db.InsertFuturesOptionTickDataAsync(optionTickData);
    }
}
