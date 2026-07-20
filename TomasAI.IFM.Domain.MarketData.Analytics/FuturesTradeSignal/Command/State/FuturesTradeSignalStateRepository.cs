using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.State;

public class FuturesTradeSignalStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IDbContextFactory dbFactory,
    IBlackboardService blackboardService,
    ILogger<FuturesTradeSignalStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FuturesTradeSignalCommandState>
{
    /// <summary>
    /// Asynchronously loads the state associated with the specified command.
    /// </summary>
    /// <param name="command">The command for which the state is to be loaded. This parameter must not be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the state of type
    /// FuturesTradeSignalCommandState.</returns>
    public async ValueTask<FuturesTradeSignalCommandState> LoadStateAsync(ICommand command)
        => await LoadStateFromSnapshotAsync<FuturesTradeSignalCommandState, FuturesTradeSignalUpdatedEvent>(command);

    /// <summary>
    /// Saves futures trade signal state changes and denormalizes the associated domain events.
    /// </summary>
    /// <param name="context">The command actor context providing access to the actor system.</param>
    /// <param name="state">The current command state containing new events to persist.</param>
    /// <param name="command">The command that triggered the state changes.</param>
    /// <returns>A task that represents the asynchronous save and denormalization operation.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FuturesTradeSignalCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="domainEvents"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
    {
        var db = dbFactory.MarketDataDb;
        foreach (var domainEvent in domainEvents)
        {
            _ = domainEvent switch
            {
                FuturesTradeSignalUpdatedEvent e => await UpdateFuturesTradeSignalAsync(db, blackboardService, e),
                FuturesItiSignalHoldTradeChangedEvent e => await PostHoldTradeChangedEventAsync(e),
                _ => false
            };
        }

        ///
        static async ValueTask<bool> UpdateFuturesTradeSignalAsync(
            IMarketDataDbContext db, IBlackboardService blackboardService, FuturesTradeSignalUpdatedEvent e)
        {
            var tradeSignal = e.FuturesTradeSignal ?? throw new InvalidOperationException("FuturesTradeSignal payload is required.");
            var sequenceId = blackboardService.SequenceCounter.Get(SequenceName.FuturesTradeSignal_SequenceId);
            tradeSignal = tradeSignal with { SequenceId = sequenceId };
            await db.InsertFuturesTradeSignalAsync(tradeSignal);
            return true;
        }

        static async ValueTask<bool> PostHoldTradeChangedEventAsync(FuturesItiSignalHoldTradeChangedEvent e)
        {
            await ValueTask.CompletedTask;
            return true;
        }
    }
}
