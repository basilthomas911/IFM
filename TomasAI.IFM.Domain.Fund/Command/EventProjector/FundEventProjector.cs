using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Command.Actor;

namespace TomasAI.IFM.Domain.Fund.Command.EventProjector;

public class FundEventProjector(
    IDbContextFactory dbFactory,
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService,
    ILogger<FundEventProjector> logger) : BaseEventProjector<FundCommandActor>(
       durableReplayQueue, dbEventSource, blackboardService, logger)
{
    static readonly Type[] _projectedEventTypes =
    [
        typeof(FundCreatedEvent),
        typeof(OrderAddedToFundEvent),
        typeof(TradeAddedToFundOrderEvent),
        typeof(OrderRemovedFromFundEvent),
        typeof(TradeRemovedFromFundOrderEvent),
        typeof(FundOrderTradeStateChangedEvent),
        typeof(FundOrderClosedEvent),
        typeof(FundMaxProfitGeneratedEvent)
    ];

    EventProjectorBuilder ProjectionBuilder => CreateProjectionBuilder();

    public override string ActorName => $"{typeof(FundCommandActor).Name}";
    public override string ProjectorName => $"{typeof(FundEventProjector).Name}";
    public override string DurableProcessQueueName => $"{ActorName}.{ProjectorName}.ProcessQueue";
    public override string DurableReplayQueueName => $"{ActorName}.{ProjectorName}.ReplayQueue";
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _projectedEventTypes;

    /// <summary>
    /// Processes the domain event and projects it to the database.
    /// </summary>
    /// <param name="domainEvent"></param>
    /// <returns></returns>
    public override async ValueTask ProcessDomainEventAsync(IEvent domainEvent)
    {
        try
        {
            var db = dbFactory.FundDb;
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
                FundMaxProfitGeneratedEvent e => await ProjectionBuilder.RunAsync<FundMaxProfitGeneratedEvent, FundMaxProfitGeneratedCompleteEvent, FundMaxProfitGeneratedFailEvent, FundId>(
                    e, _ => Task.CompletedTask),
                _ => false
            };
        }
        catch(Exception ex)
        {
            Logger.LogError(ex, "{ProcessQueue}: error processing event projection for: {EventName}", DurableProcessQueueName, domainEvent.GetType().Name);
            throw;
        }
    }
}
