using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.State;

/// <summary>
/// Provides functionality to manage the state of futures closing price data, including loading state from snapshots
/// and saving state changes. This repository is designed to work with event-sourced actors.
/// </summary>
/// <remarks>This class extends <see cref="BaseEventSourceActorRepository"/> and implements <see
/// cref="IEventSourceActorStateRepository{FuturesClosingPriceCommandState}"/> to provide specialized behavior for managing <see
/// cref="FuturesClosingPriceCommandState"/> entities. It relies on an event-sourcing pattern to persist and retrieve
/// state.</remarks>
/// <param name="aggregateFactory"></param>
/// <param name="dbEventSource"></param>
/// <param name="actorService"></param>
/// <param name="db"></param>
/// <param name="logger"></param>
public class FuturesClosingPriceStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IDbContextFactory dbFactory,
    ILogger<FuturesClosingPriceStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FuturesClosingPriceCommandState>
{
    /// <summary>
    /// Asynchronously loads the state associated with the specified command.
    /// </summary>
    /// <remarks>This method initializes and retrieves an empty state for the given command. Use this method
    /// when a fresh state is required for command processing.</remarks>
    /// <param name="command">The command for which to load the state. This parameter determines which command's state is initialized.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the loaded state as a <see
    /// cref="FuturesClosingPriceCommandState"/> instance.</returns>
    public async ValueTask<FuturesClosingPriceCommandState> LoadStateAsync(ICommand command)
        => await LoadStateFromSnapshotAsync<FuturesClosingPriceCommandState, FuturesClosingPriceInsertedEvent>(command);

    /// <summary>
    /// save futures closing price state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesClosingPriceCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the futures closing price state
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
                FuturesClosingPriceInsertedEvent e => await UpdateReadModelAsync<FuturesClosingPriceInsertedEvent, FuturesClosingPriceInsertedCompleteEvent, FuturesClosingPriceInsertedFailEvent, FuturesDataId>(
                    context, e, async () => await InsertFuturesClosingPriceAsync(db, e)),
                _ => false
            };
        }

        static async ValueTask InsertFuturesClosingPriceAsync(IMarketDataDbContext db, FuturesClosingPriceInsertedEvent e)
            => await db.InsertFuturesClosingPriceAsync(new FuturesClosingPriceReadModel(
                        contractId: e.FuturesClosingPriceId.ContractId,
                        valueDate: e.FuturesClosingPriceId.ValueDate,
                        closingPrice: e.ClosingPrice,
                        createdOn: e.CreatedOn,
                        createdBy: e.CreatedBy));
    }
}
