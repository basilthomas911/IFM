using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command.State;

/// <summary>
/// Provides functionality to manage the state of option trades, including loading state from snapshots and saving
/// state changes. This repository is designed to work with event-sourced actors.
/// </summary>
/// <remarks>This class extends <see cref="BaseEventSourceActorRepository"/> and implements <see
/// cref="IEventSourceActorStateRepository{OptionTradeCommandState}"/> to provide specialized behavior for managing <see
/// cref="OptionTradeCommandState"/> entities. It relies on an event-sourcing pattern to persist and retrieve
/// state.</remarks>
/// <param name="aggregateFactory">The factory used to create event source actor state instances from persisted snapshots and events.</param>
/// <param name="dbEventSource">The event source database context used to persist and retrieve domain events.</param>
/// <param name="actorService">The actor service that provides access to actor infrastructure for posting events to other actors.</param>
/// <param name="db">The trade database context used to denormalize domain events into the read model.</param>
/// <param name="logger">The logger instance used to record diagnostic information during state operations.</param>
public class OptionTradeStateRepository(
    IEventSourceActorStateFactory aggregateFactory,
    IEventSourceActorDbContext dbEventSource,
    IActorService actorService,
    IDbContextFactory dbFactory,
    ILogger<OptionTradeStateRepository> logger)
    : BaseEventSourceActorRepository(aggregateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<OptionTradeCommandState>
{
    /// <summary>
    /// Loads the current option trade command state by replaying events from the most recent snapshot.
    /// </summary>
    /// <remarks>This method delegates to <see cref="BaseEventSourceActorRepository.LoadStateFromSnapshotAsync{TState, TSnapshotEvent}"/>
    /// using <see cref="OptionTradeCommandState"/> as the state type and <see cref="OptionTradeSnapshotEvent"/> as the
    /// snapshot event type. The snapshot provides a point-in-time baseline from which subsequent events are applied.</remarks>
    /// <param name="command">The command that identifies the aggregate whose state should be loaded.</param>
    /// <returns>A <see cref="ValueTask{OptionTradeCommandState}"/> containing the reconstituted option trade command state.</returns>
    public async ValueTask<OptionTradeCommandState> LoadStateAsync(ICommand command)
        => await LoadStateFromSnapshotAsync<OptionTradeCommandState, OptionTradeSnapshotEvent>(command);

    /// <summary>
    /// Persists the option trade command state and denormalizes any pending domain events into the read model.
    /// </summary>
    /// <remarks>This method delegates to <see cref="BaseEventSourceActorRepository.SaveStateAndDenormalizeEventsAsync"/>,
    /// which persists the domain events to the event store and then invokes <see cref="DenormalizeEventsAsync"/> to update
    /// the read model projections.</remarks>
    /// <param name="context">The command actor context that provides access to the actor's container and messaging infrastructure.</param>
    /// <param name="state">The current option trade command state containing pending domain events to be persisted.</param>
    /// <param name="command">The command that triggered the state change, used for correlation and auditing.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous save and denormalization operation.</returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, OptionTradeCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Updates the read model state by applying a collection of domain events to the option trade query state
    /// asynchronously.
    /// </summary>
    /// <remarks>This method processes each domain event in the provided collection and updates the read model
    /// accordingly. For <see cref="OptionTradeOrderPlacedEvent"/>, the event is posted via
    /// <see cref="BaseEventSourceActorRepository.PostEventAsync{TEvent, TEntityId}"/> and the option trade is
    /// inserted into the database. Events that require database updates are handled inline. Events that only
    /// need to be posted are acknowledged without further action since they are already persisted in the event
    /// store.</remarks>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">A collection of domain events to be denormalized and applied to the read model state.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
    {
        var db = dbFactory.TradeDb;
        foreach (var domainEvent in domainEvents)
        {
            _ = domainEvent switch
            {
                OptionTradeOrderPlacedEvent e => await PostEventAndInsertOptionTradeAsync(db, context, e),
                OptionTradeSnapshotEvent => true,
                OptionTradePositionOpenedEvent => true,
                OptionTradePositionClosedEvent => true,
                OptionTradeEndOfDayProcessedEvent => true,
                OptionTradeSpreadDistributionStatisticsUpdatedEvent => true,
                OptionTradeSpreadDataInsertedEvent e => await InsertOptionTradeSpreadDataAsync(db, e),
                OptionTradeSpreadBarDataInsertedEvent e => await InsertOptionTradeSpreadBarDataAsync(db, e),
                OptionTradeSpreadBarDataDeletedEvent e => await DeleteOptionTradeSpreadBarDataAsync(db, e),
                TradePositionAddedEvent e => await InsertTradePositionAsync(db, e),
                TradePositionUpdatedEvent e => await UpdateTradePositionAsync(db, e),
                TradePositionStatusUpdatedEvent e => await UpdateTradePositionStatusAsync(db, e),
                OptionTradeDeletedEvent e => await DeleteOptionTradeAsync(db, e),
                OptionTradeDailyProfitTargetUpdatedEvent e => await UpdateTradeLimitDailyProfitTargetAsync(db, e),
                _ => false
            };
        }
    }

    /// <summary>
    /// Posts the option trade order placed event to downstream actors and inserts the option trade into the read model.
    /// </summary>
    /// <remarks>This method first posts the event via <see cref="BaseEventSourceActorRepository.PostEventAsync{TEvent, TEntityId}"/>
    /// using <see cref="OptionTradeEntityId"/> as the entity identifier, then inserts the trade into the database.
    /// The <see cref="OptionTradeOrderPlacedEvent"/> is the only event type that implements
    /// <see cref="IEvent{OptionTradeEntityId}"/>, enabling typed event posting.</remarks>
    /// <param name="context">The command actor context used to post the event to downstream actors.</param>
    /// <param name="e">The option trade order placed event containing the full trade details.</param>
    /// <returns><see langword="true"/> after the event has been posted and the trade inserted successfully.</returns>
    async ValueTask<bool> PostEventAndInsertOptionTradeAsync(ITradeDbContext db, ICommandActorContext context, OptionTradeOrderPlacedEvent e)
    {
        await PostEventAsync<OptionTradeOrderPlacedEvent, OptionTradeEntityId>(context, e);
        await InsertOptionTradeAsync(db, e);
        return true;

        static async ValueTask InsertOptionTradeAsync(ITradeDbContext db, OptionTradeOrderPlacedEvent e)
        {
            var optionTrade = new OptionTradeReadModel(
                    orderId: e.OptionTrade.OrderId,
                    tradeId: e.OptionTrade.TradeId,
                    tradeStrategy: e.OptionTrade.TradeStrategy,
                    tradeDate: e.OptionTrade.TradeDate,
                    maturityDate: e.OptionTrade.MaturityDate,
                    tradeType: e.OptionTrade.TradeType,
                    tradeState: e.OptionTrade.TradeState,
                    tradeAction: e.OptionTrade.TradeAction,
                    underlyingContractId: e.OptionTrade.UnderlyingContractId,
                    underlyingAssetType: e.OptionTrade.UnderlyingAssetType,
                    isPrimaryTrade: e.OptionTrade.IsPrimaryTrade,
                    isHedgeTrade: e.OptionTrade.IsHedgeTrade,
                    createdOn: e.CreatedOn,
                    createdBy: e.CreatedBy,
                    updatedOn: e.CreatedOn,
                    updatedBy: e.CreatedBy)
                .AddOptionLegs(e.OptionTrade.OptionLegs ?? [])
                .AddTradePosition(e.OptionTrade.TradePositions ?? [])
                .SetTradeLimit(e.OptionTrade.TradeLimit!)
                .AddTradeTypeLimits(e.OptionTrade.TradeTypeLimits ?? [])
                .AddTradeFills(e.OptionTrade.TradeFills ?? []);
            await db.InsertOptionTradeAsync(optionTrade);
        }
    }

    /// <summary>
    /// Inserts option trade spread data into the trade database from the specified event.
    /// </summary>
    /// <param name="e">The event containing the option trade spread data to insert.</param>
    /// <returns><see langword="true"/> after the spread data has been inserted successfully.</returns>
    static async ValueTask<bool> InsertOptionTradeSpreadDataAsync(ITradeDbContext db, OptionTradeSpreadDataInsertedEvent e)
    {
        await db.InsertOptionTradeSpreadDataAsync(e.OptionTradeSpreadData);
        return true;
    }

    /// <summary>
    /// Inserts option trade spread bar data into the trade database from the specified event.
    /// </summary>
    /// <param name="e">The event containing the option trade spread bar data to insert.</param>
    /// <returns><see langword="true"/> after the spread bar data has been inserted successfully.</returns>
    static async ValueTask<bool> InsertOptionTradeSpreadBarDataAsync(ITradeDbContext db, OptionTradeSpreadBarDataInsertedEvent e)
    {
        await db.InsertOptionTradeSpreadBarDataAsync(e.OptionTradeSpreadBarData);
        return true;
    }

    /// <summary>
    /// Deletes option trade spread bar data from the trade database for the order, trade, value date, and trade type
    /// specified in the event.
    /// </summary>
    /// <param name="e">The event identifying the spread bar data to delete by order ID, trade ID, value date, and trade type.</param>
    /// <returns><see langword="true"/> after the spread bar data has been deleted successfully.</returns>
    static async ValueTask<bool> DeleteOptionTradeSpreadBarDataAsync(ITradeDbContext db, OptionTradeSpreadBarDataDeletedEvent e)
    {
        await db.DeleteOptionTradeSpreadBarDataAsync(e.OrderId, e.TradeId, e.ValueDate, e.TradeType);
        return true;
    }

    /// <summary>
    /// Inserts a new trade position into the trade database from the specified event.
    /// </summary>
    /// <param name="e">The event containing the trade position to insert.</param>
    /// <returns><see langword="true"/> after the trade position has been inserted successfully.</returns>
    static async ValueTask<bool> InsertTradePositionAsync(ITradeDbContext db, TradePositionAddedEvent e)
    {
        await db.InsertTradePositionAsync(e.TradePosition);
        return true;
    }

    /// <summary>
    /// Updates trade positions in the trade database based on the change source specified in the event.
    /// </summary>
    /// <remarks>The update behavior varies by <see cref="TradePositionChangeSourceType"/>:
    /// <list type="bullet">
    /// <item><description><see cref="TradePositionChangeSourceType.PutCreditSpreadLeg"/> — inserts the put trade position.</description></item>
    /// <item><description><see cref="TradePositionChangeSourceType.CallCreditSpreadLeg"/> — inserts the call trade position.</description></item>
    /// <item><description><see cref="TradePositionChangeSourceType.SpreadDistributionStatistics"/> — inserts both the put and call trade positions.</description></item>
    /// </list>
    /// All other change source types are ignored.</remarks>
    /// <param name="e">The event containing the trade position change source and the put and/or call trade positions to update.</param>
    /// <returns><see langword="true"/> after the trade position update has been applied.</returns>
    static async ValueTask<bool> UpdateTradePositionAsync(ITradeDbContext db, TradePositionUpdatedEvent e)
    {
        await (e.TradePositionChangeSource switch
        {
            TradePositionChangeSourceType.PutCreditSpreadLeg => db.InsertTradePositionAsync(e.PutTradePosition!),
            TradePositionChangeSourceType.CallCreditSpreadLeg => db.InsertTradePositionAsync(e.CallTradePosition!),
            TradePositionChangeSourceType.SpreadDistributionStatistics => db.InsertTradePositionAsync([e.PutTradePosition!, e.CallTradePosition!]),
            _ => Task.CompletedTask
        });
        return true;
    }

    /// <summary>
    /// Updates the status of a trade position in the trade database using the status transition details from the event.
    /// </summary>
    /// <param name="e">The event containing the order ID, trade ID, trade type, value date, days to expiry, old and new
    /// trade statuses, and audit fields for the status update.</param>
    /// <returns><see langword="true"/> after the trade position status has been updated successfully.</returns>
    static async ValueTask<bool> UpdateTradePositionStatusAsync(ITradeDbContext db, TradePositionStatusUpdatedEvent e)
    {
        await db.UpdateTradePositionStatusAsync(
            orderId: e.OrderId,
            tradeId: e.TradeId,
            tradeType: e.TradeType,
            valueDate: e.ValueDate,
            daysToExpiry: e.DaysToExpiry,
            oldTradeStatus: e.OldTradeStatus,
            newTradeStatus: e.NewTradeStatus,
            updatedOn: e.UpdatedOn,
            updatedBy: e.UpdatedBy);
        return true;
    }

    /// <summary>
    /// Deletes an option trade and its associated data from the trade database for the order and trade specified in the event.
    /// </summary>
    /// <param name="e">The event identifying the option trade to delete by order ID and trade ID.</param>
    /// <returns><see langword="true"/> after the option trade has been deleted successfully.</returns>
    static async ValueTask<bool> DeleteOptionTradeAsync(ITradeDbContext db, OptionTradeDeletedEvent e)
    {
        await db.DeleteOptionTradeAsync(e.OrderId, e.TradeId);
        return true;
    }

    /// <summary>
    /// Updates the daily profit target for a trade limit in the trade database using the values from the event.
    /// </summary>
    /// <param name="e">The event containing the trade ID, trade type, new daily profit target value, and audit fields.</param>
    /// <returns><see langword="true"/> after the daily profit target has been updated successfully.</returns>
    static async ValueTask<bool> UpdateTradeLimitDailyProfitTargetAsync(ITradeDbContext db, OptionTradeDailyProfitTargetUpdatedEvent e)
    {
        await db.UpdateTradeLimitDailyProfitTarget(
            tradeId: e.TradeId,
            tradeType: e.TradeType,
            dailyProfitTarget: e.DailyProfitTarget,
            updatedOn: e.UpdatedOn,
            updatedBy: e.UpdatedBy);
        return true;
    }
}
