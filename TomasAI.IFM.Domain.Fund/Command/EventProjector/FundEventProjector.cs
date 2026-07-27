using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Command.Actor;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Command.EventProjector;

public class FundEventProjector(
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource,
    IDbContextFactory dbFactory,
    IBlackboardService blackboardService,
    ICommandActorContext commandActorContext,
    ILogger<FundEventProjector> logger) : BaseEventProjector<FundCommandActor>(
       durableReplayQueue, dbEventSource,  dbFactory, blackboardService, commandActorContext, logger)
{
    EventProjectorBuilder ProjectionBuilder => CreateProjectionBuilder();
    public override string DurableProcessQueueName => $"FundEventProjector.ProcessQueue";
    public override string DurableReplayQueueName => $"FundEventProjector.ReplayQueue";

    /// <summary>
    /// Processes the domain event and projects it to the database.
    /// </summary>
    /// <param name="domainEvent"></param>
    /// <returns></returns>
    public override async ValueTask ProcessDomainEventAsync(IEvent domainEvent)
    {
        try
        {
            var db = DbFactory.FundDb;
            Logger.LogInformation("{ProcessQueue}: processing event projection for: {EventName}", DurableProcessQueueName, domainEvent.GetType().Name);
            _ = domainEvent switch
            {
                FundCreatedEvent e => await ProjectionBuilder.RunAsync<FundCreatedEvent, FundCreatedCompleteEvent, FundCreatedFailEvent, FundId>(
                     e, o => db.InsertFundAsync(o.NewFund)),
                OrderAddedToFundEvent e => await ProjectionBuilder.RunAsync<OrderAddedToFundEvent, OrderAddedToFundCompleteEvent, OrderAddedToFundFailEvent, FundId>(
                     e, o => db.InsertFundOrderAsync(o.FundOrder)),
                TradeAddedToFundOrderEvent e => await ProjectionBuilder.RunAsync<TradeAddedToFundOrderEvent, TradeAddedToFundOrderCompleteEvent, TradeAddedToFundOrderFailEvent, FundId>(
                     e, o => db.InsertFundOrderTradeAsync(o.FundOrderTrade)),
                OrderRemovedFromFundEvent e => await ProjectionBuilder.RunAsync<OrderRemovedFromFundEvent, OrderRemovedFromFundCompleteEvent, OrderRemovedFromFundFailEvent, FundId>(
                    e, o => db.DeleteFundOrderAsync(o.FundOrderId.FundId, o.FundOrderId.OrderId)),
                TradeRemovedFromFundOrderEvent e => await ProjectionBuilder.RunAsync<TradeRemovedFromFundOrderEvent, TradeRemovedFromFundOrderCompleteEvent, TradeRemovedFromFundOrderFailEvent, FundId>(
                    e, o => db.DeleteFundOrderTradeAsync(o.FundOrderTradeId.FundId, o.FundOrderTradeId.OrderId, o.FundOrderTradeId.TradeId)),
                FundOrderTradeStateChangedEvent e => await ProjectionBuilder.RunAsync<FundOrderTradeStateChangedEvent, FundOrderTradeStateChangedCompleteEvent, FundOrderTradeStateChangedFailEvent, FundId>(
                    e, o => db.UpdateFundOrderTradeStateAsync(o.FundOrderTradeId.FundId, o.FundOrderTradeId.OrderId, o.FundOrderTradeId.TradeId, o.TradeState, o.UpdatedOn, o.UpdatedBy)),
                FundOrderClosedEvent e => await ProjectionBuilder.RunAsync<FundOrderClosedEvent, FundOrderClosedCompleteEvent, FundOrderClosedFailEvent, FundId>(
                    e, o => db.UpdateFundOrderStatusAsync(o.FundOrderId.FundId, o.FundOrderId.OrderId, OrderStatus.Closed)),
                FundMaxProfitGeneratedEvent e => await PostEventAsync<FundMaxProfitGeneratedEvent, FundId>(e),
                _ => false
            };
        }
        catch(Exception ex)
        {
            Logger.LogError(ex, "{ProcessQueue}: error processing event projection for: {EventName}", DurableProcessQueueName, domainEvent.GetType().Name);
            await LogExceptionAsync(ex, domainEvent);
        }
    }
}
