using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;

public class FuturesRsiSignalStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IDbContextFactory dbFactory,
    ILogger<FuturesRsiSignalStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FuturesRsiSignalCommandState>
{
    /// <summary>
    /// Asynchronously loads the state associated with the specified command.
    /// </summary>
    /// <param name="command">The command for which the state is to be loaded. This parameter must not be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the state of type
    /// FuturesRsiSignalCommandState.</returns>
    public async ValueTask<FuturesRsiSignalCommandState> LoadStateAsync(ICommand command)
        => await LoadStateFromSnapshotAsync<FuturesRsiSignalCommandState, FuturesRsiSignalStartedEvent>(command);

    /// <summary>
    /// Saves futures RSI signal state changes and denormalizes the associated domain events.
    /// </summary>
    /// <param name="context">The command actor context providing access to the actor system.</param>
    /// <param name="state">The current command state containing new events to persist.</param>
    /// <param name="command">The command that triggered the state changes.</param>
    /// <returns>A task that represents the asynchronous save and denormalization operation.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesRsiSignalCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Denormalizes domain events related to futures RSI signals and updates the read model in the database.
    /// </summary>
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
                FuturesRsiSignalGeneratedEvent e => await UpdateReadModelAsync<FuturesRsiSignalGeneratedEvent, FuturesRsiSignalGeneratedCompleteEvent, FuturesRsiSignalGeneratedFailEvent, FuturesRsiSignalEntityId>(
                    context, e, () => InsertFuturesRsiSignalAsync(db, e.FuturesRsiSignal)),
                FuturesRsiDailySignalGeneratedEvent e => await UpdateReadModelAsync<FuturesRsiDailySignalGeneratedEvent, FuturesRsiDailySignalGeneratedCompleteEvent, FuturesRsiDailySignalGeneratedFailEvent, FuturesRsiDailySignalEntityId>(
                    context, e, () => InsertFuturesRsiSignalAsync(db, e.FuturesRsiSignal)),
                FuturesRsiSignalsGeneratedEvent e => await PostEventAsync<FuturesRsiSignalsGeneratedEvent, FuturesRsiSignalEntityId>(context, e),
                FuturesRsiSignalStartedEvent e => await PostEventAsync<FuturesRsiSignalStartedEvent, FuturesRsiSignalEntityId>(context, e),
                FuturesRsiSignalStoppedEvent e => await PostEventAsync<FuturesRsiSignalStoppedEvent, FuturesRsiSignalEntityId>(context, e),
                _ => false
            };
        }

        static async ValueTask InsertFuturesRsiSignalAsync(IMarketDataDbContext db, FuturesRsiSignalReadModel futuresRsiSignal)
            => await db.InsertFuturesRsiSignalAsync(futuresRsiSignal);
    }
}
