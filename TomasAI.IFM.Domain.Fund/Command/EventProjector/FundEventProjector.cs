using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Command.Actor;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Fund.Command.EventProjector;

public class FundEventProjector(
    IDbContextFactory dbFactory,
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService,
    ILogger<FundEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null) : BaseEventProjector<FundCommandActor>(
       durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    static readonly ImmutableArray<Type> _projectedEventTypes =
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
    readonly ImmutableArray<EventProjectionDescriptor> _projectionDescriptors =
    [
        Describe<FundCreatedEvent, FundCreatedCompleteEvent, FundCreatedFailEvent>(
            e => dbFactory.FundDb.InsertFundAsync(e.NewFund)),
        Describe<OrderAddedToFundEvent, OrderAddedToFundCompleteEvent, OrderAddedToFundFailEvent>(
            e => dbFactory.FundDb.InsertFundOrderAsync(e.FundOrder)),
        Describe<TradeAddedToFundOrderEvent, TradeAddedToFundOrderCompleteEvent, TradeAddedToFundOrderFailEvent>(
            e => dbFactory.FundDb.InsertFundOrderTradeAsync(e.FundOrderTrade)),
        Describe<OrderRemovedFromFundEvent, OrderRemovedFromFundCompleteEvent, OrderRemovedFromFundFailEvent>(
            e => dbFactory.FundDb.DeleteFundOrderAsync(e.FundOrderId.FundId, e.FundOrderId.OrderId)),
        Describe<TradeRemovedFromFundOrderEvent, TradeRemovedFromFundOrderCompleteEvent, TradeRemovedFromFundOrderFailEvent>(
            e => dbFactory.FundDb.DeleteFundOrderTradeAsync(
                e.FundOrderTradeId.FundId,
                e.FundOrderTradeId.OrderId,
                e.FundOrderTradeId.TradeId)),
        Describe<FundOrderTradeStateChangedEvent, FundOrderTradeStateChangedCompleteEvent, FundOrderTradeStateChangedFailEvent>(
            e => dbFactory.FundDb.UpdateFundOrderTradeStateAsync(
                e.FundOrderTradeId.FundId,
                e.FundOrderTradeId.OrderId,
                e.FundOrderTradeId.TradeId,
                e.TradeState,
                e.UpdatedOn,
                e.UpdatedBy)),
        Describe<FundOrderClosedEvent, FundOrderClosedCompleteEvent, FundOrderClosedFailEvent>(
            e => dbFactory.FundDb.UpdateFundOrderStatusAsync(
                e.FundOrderId.FundId,
                e.FundOrderId.OrderId,
                OrderStatus.Closed)),
        Describe<FundMaxProfitGeneratedEvent, FundMaxProfitGeneratedCompleteEvent, FundMaxProfitGeneratedFailEvent>(
            static _ => Task.CompletedTask)
    ];

    public override string ActorName => $"{typeof(FundCommandActor).Name}";
    public override string ProjectorName => $"{typeof(FundEventProjector).Name}";
    public override string DurableProcessQueueName => $"{ActorName}.{ProjectorName}.ProcessQueue";
    public override string DurableReplayQueueName => $"{ActorName}.{ProjectorName}.ReplayQueue";
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _projectedEventTypes;
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _projectionDescriptors;

    static EventProjectionDescriptor Describe<TEvent, TComplete, TFail>(
        Func<TEvent, Task> applyAsync,
        bool useDurableReplay = true)
        where TEvent : class, IEvent<FundId>
        where TComplete : class, ICompleteEvent<FundId>
        where TFail : class, IErrorEvent<FundId>
        => new(
            typeof(TEvent),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            async (domainEvent, _) =>
            {
                await applyAsync((TEvent)domainEvent).ConfigureAwait(false);
                return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
            },
            domainEvent => ((TEvent)domainEvent).ToCompleteEvent<TComplete, FundId>(),
            (domainEvent, exception) => ((TEvent)domainEvent).ToFailEvent<TFail, FundId>(exception),
            useDurableReplay: useDurableReplay);
}
