using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Realtime.Actor;

/// <summary>
/// Routes live futures trades through the actor-centric bar model and forwards completed bars to the Command actor.
/// This Realtime actor owns no durable state and performs no projection.
/// </summary>
public sealed class FuturesTradeSessionBarPublisherRealtimeActor(
    IRealtimeActorContext<FuturesTradeSessionBarPublisherRealtimeActor> actorContext)
    : BaseEventActor<FuturesTradeSessionBarPublisherRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the Realtime actor mailbox name.</summary>
    public const string ActorName = FuturesTradeSessionBarClosedRealtimeEvent.Actor;

    static readonly ActorTypeId MarketPriceRoute = new(
        ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor,
        FuturesMarketPriceUpdatedRealtimeEvent.Verb);

    readonly IFuturesTradeSessionBarPublisherRealtimeContext context = IsArgumentNull.Set(
        actorContext as IFuturesTradeSessionBarPublisherRealtimeContext,
        nameof(actorContext))!;
    readonly ILogger<FuturesTradeSessionBarPublisherRealtimeActor> logger = IsArgumentNull.Set(actorContext.Logger);
    readonly CancellationTokenSource barrierStopping = new();
    Task barrierLoop = Task.CompletedTask;

    readonly Dictionary<Type, Func<IEvent, IFuturesTradeSessionBarPublisherRealtimeContext, ILogger, ValueTask<bool>>>
        receiveMap = new()
        {
            [typeof(FuturesMarketPriceUpdatedRealtimeEvent)] = static (@event, context, logger) =>
                ((FuturesMarketPriceUpdatedRealtimeEvent)@event).ExecuteAsync(context, logger),
            [typeof(FuturesTradeSessionBarPublisherBarrierRealtimeEvent)] = static (@event, context, logger) =>
                ((FuturesTradeSessionBarPublisherBarrierRealtimeEvent)@event).ExecuteAsync(context, logger)
        };

    /// <summary>Registers the market-price route and starts the server-owned interval barrier.</summary>
    protected override ValueTask OnStartup(
        IEventActorContext<FuturesTradeSessionBarPublisherRealtimeActor> actorContext)
    {
        actorContext.AddRealtimeRouter(MarketPriceRoute, Id);
        barrierLoop = RunBarrierLoopAsync(actorContext, context.TimeProvider, barrierStopping.Token);
        return ValueTask.CompletedTask;
    }

    /// <summary>Releases the market-price route and stops the interval barrier.</summary>
    protected override async ValueTask OnShutdown(
        IEventActorContext<FuturesTradeSessionBarPublisherRealtimeActor> actorContext)
    {
        actorContext.RemoveRealtimeRouter(MarketPriceRoute, Id);
        await barrierStopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await barrierLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (barrierStopping.IsCancellationRequested)
        {
        }
    }

    /// <summary>Parses routed trade updates and the private publisher barrier event.</summary>
    protected override IEvent ParseMessage(
        IEventActorContext<FuturesTradeSessionBarPublisherRealtimeActor> actorContext,
        IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Realtime, Name: ActorName }) return default!;
        return message.Subject.Verb switch
        {
            FuturesMarketPriceUpdatedRealtimeEvent.Verb =>
                message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!,
            FuturesTradeSessionBarPublisherBarrierRealtimeEvent.Verb =>
                message.AsEvent<FuturesTradeSessionBarPublisherBarrierRealtimeEvent>()!,
            _ => default!
        };
    }

    /// <summary>Dispatches each supported event to its dedicated extension handler.</summary>
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesTradeSessionBarPublisherRealtimeActor> actorContext,
        IEvent @event)
    {
        if (!receiveMap.TryGetValue(@event.GetType(), out var handler))
            throw new InvalidOperationException($"Unable to resolve {ActorName} realtime event from {@event.Subject}.");
        _ = await handler(@event, context, logger).ConfigureAwait(false);
    }

    static async Task RunBarrierLoopAsync(
        IEventActorContext<FuturesTradeSessionBarPublisherRealtimeActor> actorContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await actorContext.SendAsync<FuturesTradeSessionBarPublisherBarrierRealtimeEvent, ActorEntityId>(
                FuturesTradeSessionBarPublisherBarrierRealtimeEvent.Create(timeProvider.GetUtcNow()))
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesTradeSessionBarPublisherRealtimeActor> actorContext,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) => await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, actorContext).ConfigureAwait(false);
}
