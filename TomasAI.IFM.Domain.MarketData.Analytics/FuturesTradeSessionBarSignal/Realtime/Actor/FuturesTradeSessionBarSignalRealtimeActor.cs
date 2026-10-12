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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The actor lifecycle owns the barrier registration: OnShutdown awaits the loop and disposes its cancellation source in finally; OnStartup creates a fresh source for restart.")]
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

    readonly IFuturesTradeSessionBarSignalRealtimeContext context = IsArgumentNull.Named(actorContext as IFuturesTradeSessionBarSignalRealtimeContext, nameof(actorContext))!;
    readonly ILogger<FuturesTradeSessionBarSignalRealtimeActor> logger = IsArgumentNull.Set(actorContext.Logger);
    CancellationTokenSource? barrierStopping;
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
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.AddRealtimeRouter(MarketPriceRoute, Id, ResolveAccumulatorEntityId);
        barrierStopping = new CancellationTokenSource();
        barrierLoop = RunBarrierLoopAsync(
            context,
            this.context.Calendar,
            this.context.TimeProvider,
            barrierStopping.Token);
        return ValueTask.CompletedTask;
    }

    /// <summary>Releases the market-price route and stops the interval barrier.</summary>
    protected override async ValueTask OnShutdown(
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.RemoveRealtimeRouter(MarketPriceRoute, Id);
        var stopping = barrierStopping;
        if (stopping is null) return;
        await stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await barrierLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // The publisher barrier loop completed after its stop request.
        }
        finally
        {
            stopping.Dispose();
            barrierStopping = null;
            barrierLoop = Task.CompletedTask;
        }
    }

    /// <summary>Parses routed trade updates and the private publisher barrier event.</summary>
    protected override IEvent ParseMessage(
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> context,
        IActorMessage message)
        => ParseMappedRealtimeEvent(context, message, _parseMap);

    /// <summary>Dispatches each supported event to its dedicated extension handler.</summary>
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> context,
        IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        _ = await handler(@event, this.context, logger).ConfigureAwait(false);
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
        IEventActorContext<FuturesTradeSessionBarSignalRealtimeActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception ex) => await ex.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
