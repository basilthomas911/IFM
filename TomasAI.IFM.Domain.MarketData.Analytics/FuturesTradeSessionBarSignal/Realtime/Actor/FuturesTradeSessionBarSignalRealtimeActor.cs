using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;

/// <summary>
/// Routes live futures trades through the actor-centric bar model and forwards completed bars to the Command actor.
/// This Realtime actor owns no durable state and performs no projection.
/// </summary>
public sealed class FuturesTradeSessionBarSignalRealtimeActor(
    IRealtimeActorContext<FuturesTradeSessionBarSignalRealtimeActor> actorContext)
    : BaseEventActor<FuturesTradeSessionBarSignalRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the Realtime actor mailbox name.</summary>
    public const string ActorName = FuturesTradeSessionBarClosedRealtimeEvent.Actor;

    static readonly ActorTypeId MarketPriceRoute = new(
        ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor,
        FuturesMarketPriceUpdatedRealtimeEvent.Verb);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesMarketPriceUpdatedRealtimeEvent.Verb] =
                message => message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!,
            [FuturesTradeSessionBarSignalBarrierRealtimeEvent.Verb] =
                message => message.AsEvent<FuturesTradeSessionBarSignalBarrierRealtimeEvent>()!
        };

    readonly IFuturesTradeSessionBarSignalRealtimeContext context = IsArgumentNull.Set(
        actorContext as IFuturesTradeSessionBarSignalRealtimeContext,
        nameof(actorContext))!;
    readonly ILogger<FuturesTradeSessionBarSignalRealtimeActor> logger = IsArgumentNull.Set(actorContext.Logger);
    readonly CancellationTokenSource barrierStopping = new();
    Task barrierLoop = Task.CompletedTask;

    readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesTradeSessionBarSignalRealtimeContext, ILogger, ValueTask<bool>>>
        _receiveMap = new Dictionary<Type, Func<IEvent, IFuturesTradeSessionBarSignalRealtimeContext, ILogger, ValueTask<bool>>>
        {
            [typeof(FuturesMarketPriceUpdatedRealtimeEvent)] = static (@event, context, logger) =>
                ((FuturesMarketPriceUpdatedRealtimeEvent)@event).ExecuteAsync(context, logger),
            [typeof(FuturesTradeSessionBarSignalBarrierRealtimeEvent)] = static (@event, context, logger) =>
                ((FuturesTradeSessionBarSignalBarrierRealtimeEvent)@event).ExecuteAsync(context, logger)
        };

    /// <summary>Registers the market-price route and starts the server-owned interval barrier.</summary>
    protected override ValueTask OnStartup(
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> actorContext)
    {
        actorContext.AddRealtimeRouter(MarketPriceRoute, Id, ResolveAccumulatorEntityId);
        barrierLoop = RunBarrierLoopAsync(
            actorContext,
            context.Calendar,
            context.TimeProvider,
            barrierStopping.Token);
        return ValueTask.CompletedTask;
    }

    /// <summary>Releases the market-price route and stops the interval barrier.</summary>
    protected override async ValueTask OnShutdown(
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> actorContext)
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
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> actorContext,
        IActorMessage message)
        => ParseMappedRealtimeEvent(actorContext, message, _parseMap);

    /// <summary>Dispatches each supported event to its dedicated extension handler.</summary>
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> actorContext,
        IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(actorContext);
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        _ = await handler(@event, context, logger).ConfigureAwait(false);
    }

    static async Task RunBarrierLoopAsync(
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> actorContext,
        IMarketSessionCalendar calendar,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!actorContext.IsReady)
                return;
            var barrierUtc = timeProvider.GetUtcNow();
            var entityId = new FuturesTradeSessionBarAccumulatorEntityId(
                calendar.GetValueDate(barrierUtc));
            await actorContext.SendAsync<
                    FuturesTradeSessionBarSignalBarrierRealtimeEvent,
                    FuturesTradeSessionBarAccumulatorEntityId>(
                    FuturesTradeSessionBarSignalBarrierRealtimeEvent.Create(barrierUtc, entityId))
                .ConfigureAwait(false);
        }
    }

    static string ResolveAccumulatorEntityId(ActorSubject source)
    {
        var sourceEntityId = TickDataEntityId.Parse(source.EntityId);
        return new FuturesTradeSessionBarAccumulatorEntityId(sourceEntityId.ValueDate).Format();
    }

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> actorContext,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) => await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, actorContext).ConfigureAwait(false);
}
