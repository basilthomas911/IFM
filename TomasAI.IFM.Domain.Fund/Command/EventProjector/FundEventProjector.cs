using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Command.Actor;
using TomasAI.IFM.Domain.Fund.Command.Extensions;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Fund.Command.EventProjector;

/// <summary>
/// Projects events emitted by <see cref="FundCommandActor"/> into the Fund read model and publishes the
/// corresponding completion or failure events.
/// </summary>
/// <param name="actorContext">
/// Provides the Fund persistence services, event-source context, durable replay queue, blackboard, and logger used by
/// the projector.
/// </param>
/// <param name="reliabilityOptions">
/// Configures recovery, fencing, outbox, retry, and queue behavior. When omitted, the validated default options are
/// used.
/// </param>
/// <remarks>
/// The projector registers immutable descriptors for fund creation, order and trade membership changes, trade-state
/// changes, order closure, and maximum-profit generation. Every current descriptor uses natural-key mutation and
/// durable replay. Consequently, projection work survives process interruption and incomplete work is recovered from
/// the event source when the projector starts. A descriptor can opt out in the future by passing
/// <c>useDurableReplay: false</c> to <c>ProjectionFor</c>; such work still executes asynchronously through the bounded
/// process-local queue, but it is not recovered or replayed after process loss.
/// </remarks>
public class FundEventProjector(
    ICommandActorContext<FundCommandActor> actorContext,
    EventProjectorReliabilityOptions? reliabilityOptions = null) : BaseEventProjector<FundCommandActor>(
       actorContext.DurableReplayQueue,
       actorContext.DbEventSource,
       actorContext.BlackboardService,
       actorContext.Logger,
       reliabilityOptions)
{
    /// <summary>
    /// Contains the complete, immutable set of domain-event types accepted by this projector.
    /// </summary>
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

    /// <summary>
    /// Contains the immutable projection behavior, terminal-event factories, idempotency strategy, and delivery mode
    /// associated with each supported domain-event type.
    /// </summary>
    readonly ImmutableArray<EventProjectionDescriptor> _projectionDescriptors =
    [
        ProjectionFor<FundCreatedEvent, FundCreatedCompleteEvent, FundCreatedFailEvent>(
            e => actorContext.DbFactory.FundDb.InsertFundAsync(e.NewFund)),
        ProjectionFor<OrderAddedToFundEvent, OrderAddedToFundCompleteEvent, OrderAddedToFundFailEvent>(
            e => actorContext.DbFactory.FundDb.InsertFundOrderAsync(e.FundOrder)),
        ProjectionFor<TradeAddedToFundOrderEvent, TradeAddedToFundOrderCompleteEvent, TradeAddedToFundOrderFailEvent>(
            e => actorContext.DbFactory.FundDb.InsertFundOrderTradeAsync(e.FundOrderTrade)),
        ProjectionFor<OrderRemovedFromFundEvent, OrderRemovedFromFundCompleteEvent, OrderRemovedFromFundFailEvent>(
            e => actorContext.DbFactory.FundDb.DeleteFundOrderAsync(e.FundOrderId.FundId, e.FundOrderId.OrderId)),
        ProjectionFor<TradeRemovedFromFundOrderEvent, TradeRemovedFromFundOrderCompleteEvent, TradeRemovedFromFundOrderFailEvent>(
            e => actorContext.DbFactory.FundDb.DeleteFundOrderTradeAsync(
                e.FundOrderTradeId.FundId,
                e.FundOrderTradeId.OrderId,
                e.FundOrderTradeId.TradeId)),
        ProjectionFor<FundOrderTradeStateChangedEvent, FundOrderTradeStateChangedCompleteEvent, FundOrderTradeStateChangedFailEvent>(
            e => actorContext.DbFactory.FundDb.UpdateFundOrderTradeStateAsync(
                e.FundOrderTradeId.FundId,
                e.FundOrderTradeId.OrderId,
                e.FundOrderTradeId.TradeId,
                e.TradeState,
                e.UpdatedOn,
                e.UpdatedBy)),
        ProjectionFor<FundOrderClosedEvent, FundOrderClosedCompleteEvent, FundOrderClosedFailEvent>(
            e => actorContext.DbFactory.FundDb.UpdateFundOrderStatusAsync(
                e.FundOrderId.FundId,
                e.FundOrderId.OrderId,
                OrderStatus.Closed)),
        ProjectionFor<FundMaxProfitGeneratedEvent, FundMaxProfitGeneratedCompleteEvent, FundMaxProfitGeneratedFailEvent>(
            static _ => Task.CompletedTask)
    ];

    /// <summary>
    /// Gets the name of the command actor whose event stream supplies this projector.
    /// </summary>
    public override string ActorName => $"{typeof(FundCommandActor).Name}";

    /// <summary>
    /// Gets the stable logical name used to identify this projector in persistence, metrics, and queue resources.
    /// </summary>
    public override string ProjectorName => $"{typeof(FundEventProjector).Name}";

    /// <summary>
    /// Gets the logical name of the durable queue that accepts newly committed Fund domain events.
    /// </summary>
    public override string DurableProcessQueueName => $"{ActorName}.{ProjectorName}.ProcessQueue";

    /// <summary>
    /// Gets the logical name of the durable queue used to retry and recover incomplete Fund projections.
    /// </summary>
    public override string DurableReplayQueueName => $"{ActorName}.{ProjectorName}.ReplayQueue";

    /// <summary>
    /// Gets the immutable set of Fund domain-event types supported by this projector.
    /// </summary>
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _projectedEventTypes;

    /// <summary>
    /// Gets the immutable descriptors that define how supported Fund domain events update the read model and produce
    /// completion or failure events.
    /// </summary>
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _projectionDescriptors;

    /// <summary>
    /// Creates a projection descriptor for a Fund domain event and its corresponding terminal event contracts.
    /// </summary>
    /// <typeparam name="TEvent">The Fund domain-event type consumed by the projection.</typeparam>
    /// <typeparam name="TComplete">The event type published after the projection succeeds.</typeparam>
    /// <typeparam name="TFail">The event type published after the projection fails.</typeparam>
    /// <param name="applyAsync">The asynchronous read-model mutation performed for the domain event.</param>
    /// <param name="useDurableReplay">
    /// <see langword="true"/> to use durable processing and startup recovery; <see langword="false"/> to use the
    /// bounded process-local queue without replay after process loss.
    /// </param>
    /// <returns>
    /// An immutable descriptor configured for natural-key mutation, including completion and failure event factories.
    /// </returns>
    static EventProjectionDescriptor ProjectionFor<TEvent, TComplete, TFail>(
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
