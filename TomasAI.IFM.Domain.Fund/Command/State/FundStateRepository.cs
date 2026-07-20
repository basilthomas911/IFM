using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;


namespace TomasAI.IFM.Domain.Fund.Command.State;

/// <summary>
/// Provides a repository for managing the state of funds using event sourcing and actor-based persistence.
/// </summary>
/// <param name="stateFactory">The factory used to create actor state instances for event sourcing operations.</param>
/// <param name="dbEventSource">The database context for accessing event source data.</param>
/// <param name="actorService">The actor service responsible for managing actor lifecycles and communication.</param>
/// <param name="logger">The logger used to record diagnostic and operational information.</param>
public class FundStateRepository(
    IEventSourceActorStateFactory stateFactory,
    IEventSourceActorDbContext dbEventSource,
    IDbContextFactory dbFactory,
    IActorService actorService,
    ILogger<FundStateRepository> logger) 
    : BaseEventSourceActorRepository(stateFactory, dbEventSource, actorService, logger), IEventSourceActorStateRepository<FundCommandState>
{
    /// <summary>
    /// load fund state from snapshot event
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask<FundCommandState> LoadStateAsync(ICommand command)
        => await LoadStateFromSnapshotAsync<FundCommandState, FundCreatedEvent>(command);

    /// <summary>
    /// save fund state changes
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public async ValueTask SaveStateAsync(ICommandActorContext context, FundCommandState state, ICommand command)
       => await SaveStateAndDenormalizeEventsAsync(context, state, command);

    /// <summary>
    /// Processes a collection of domain events and updates the read model state for the fund query actor accordingly.
    /// </summary>
    /// <remarks>This method handles various fund-related domain events by updating the corresponding read
    /// model state. It should be called as part of the event handling pipeline to ensure the read model remains
    /// consistent with the domain events. The method processes each event in the provided collection
    /// sequentially.</remarks>
    /// <param name="context">The command actor context that provides access to the actor's container and state required for denormalization.</param>
    /// <param name="domainEvents">The collection of domain events to be denormalized and applied to the read model.</param>
    /// <returns>A task that represents the asynchronous denormalization operation.</returns>
    protected override async ValueTask DenormalizeEventsAsync(ICommandActorContext context, DomainEventCollection domainEvents)
    {
        var db = dbFactory.FundDb;
        foreach (var domainEvent in domainEvents)
        {
            _ = domainEvent switch
            {
                FundCreatedEvent e => await UpdateReadModelAsync<FundCreatedEvent, FundCreatedCompleteEvent, FundCreatedFailEvent, FundId>(
                    context, e, () => InsertFundAsync(db, e.NewFund)),
                OrderAddedToFundEvent e => await UpdateReadModelAsync<OrderAddedToFundEvent, OrderAddedToFundCompleteEvent, OrderAddedToFundFailEvent, FundId>(
                    context, e, () => InsertFundOrderAsync(db, e.FundOrder)),
                TradeAddedToFundOrderEvent e => await UpdateReadModelAsync<TradeAddedToFundOrderEvent, TradeAddedToFundOrderCompleteEvent, TradeAddedToFundOrderFailEvent, FundId>(
                    context, e, () => InsertFundOrderTradeAsync(db, e.FundOrderTrade)),
                OrderRemovedFromFundEvent e => await UpdateReadModelAsync<OrderRemovedFromFundEvent, OrderRemovedFromFundCompleteEvent, OrderRemovedFromFundFailEvent, FundId>(
                    context, e, () => DeleteFundOrderAsync(db, e.FundOrderId.FundId, e.FundOrderId.OrderId)),
                TradeRemovedFromFundOrderEvent e => await UpdateReadModelAsync<TradeRemovedFromFundOrderEvent, TradeRemovedFromFundOrderCompleteEvent, TradeRemovedFromFundOrderFailEvent, FundId>(
                    context, e, () => DeleteFundOrderTradeAsync(db, e.FundOrderTradeId.FundId, e.FundOrderTradeId.OrderId, e.FundOrderTradeId.TradeId)),
                FundOrderTradeStateChangedEvent e => await UpdateReadModelAsync<FundOrderTradeStateChangedEvent, FundOrderTradeStateChangedCompleteEvent, FundOrderTradeStateChangedFailEvent, FundId>(
                    context, e, () => UpdateFundOrderTradeStateAsync(db, e.FundOrderTradeId.FundId, e.FundOrderTradeId.OrderId, e.FundOrderTradeId.TradeId, e.TradeState, e.UpdatedOn, e.UpdatedBy)),
                FundOrderClosedEvent e => await UpdateReadModelAsync<FundOrderClosedEvent, FundOrderClosedCompleteEvent, FundOrderClosedFailEvent, FundId>(
                    context, e, () => UpdateFundOrderStatusAsync(db, e.FundOrderId.FundId, e.FundOrderId.OrderId, Shared.OrderStatus.Closed)),
                FundMaxProfitGeneratedEvent e => await PostEventAsync<FundMaxProfitGeneratedEvent, FundId>(context, e),
                _ => false
            };
        }

        static async ValueTask InsertFundAsync(IFundDbContext db, FundReadModel fund)
            => await db.InsertFundAsync(fund);

        static async ValueTask InsertFundOrderAsync(IFundDbContext db, FundOrderReadModel e)
            => await db.InsertFundOrderAsync(e);

        static async ValueTask InsertFundOrderTradeAsync(IFundDbContext db, FundOrderTradeReadModel e)
            => await db.InsertFundOrderTradeAsync(e);

        static async ValueTask DeleteFundOrderAsync(IFundDbContext db, int fundId, int orderId)
            => await db.DeleteFundOrderAsync(fundId, orderId);

        static async ValueTask DeleteFundOrderTradeAsync(IFundDbContext db, int fundId, int orderId, int tradeId)
            => await db.DeleteFundOrderTradeAsync(fundId, orderId, tradeId);

        static async ValueTask UpdateFundOrderTradeStateAsync(IFundDbContext db, int fundId, int orderId, int tradeId, TradeState tradeState, DateTime updatedOn, string updatedBy)
            => await db.UpdateFundOrderTradeStateAsync(fundId, orderId, tradeId, tradeState, updatedOn, updatedBy);

        static async ValueTask UpdateFundOrderStatusAsync(IFundDbContext db, int fundId, int orderId, Shared.OrderStatus orderStatus)
            => await db.UpdateFundOrderStatusAsync(fundId, orderId, orderStatus);
    }


}

