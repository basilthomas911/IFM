using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAnalyticsObservation.Realtime.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAnalyticsObservation.Realtime.Actor;

/// <summary>
/// Owns the single shared OHLCV interval schedule used by all bar-derived Analytics actors.
/// </summary>
public sealed class FuturesAnalyticsObservationRealtimeActor(
    IRealtimeActorContext<FuturesAnalyticsObservationRealtimeActor> actorContext)
    : BaseEventActor<FuturesAnalyticsObservationRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the realtime mailbox name.</summary>
    public const string ActorName = FuturesAnalyticsObservationClosedRealtimeEvent.Actor;

    static readonly ActorTypeId MarketPriceRoute = new(
        ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor,
        FuturesMarketPriceUpdatedRealtimeEvent.Verb);

    readonly IFuturesAnalyticsObservationRealtimeContext context =
        IsArgumentNull.Set(actorContext as IFuturesAnalyticsObservationRealtimeContext, nameof(actorContext))!;
    readonly FuturesAnalyticsObservationRealtimeState state = new();
    readonly CancellationTokenSource barrierStopping = new();
    Task barrierLoop = Task.CompletedTask;

    /// <summary>Starts the market-price route owned by the server actor lifecycle.</summary>
    protected override ValueTask OnStartup(IEventActorContext<FuturesAnalyticsObservationRealtimeActor> runtime)
    {
        runtime.AddRealtimeRouter(MarketPriceRoute, Id);
        barrierLoop = RunBarrierLoopAsync(runtime, context.TimeProvider, barrierStopping.Token);
        return ValueTask.CompletedTask;
    }

    /// <summary>Releases the market-price route during server shutdown.</summary>
    protected override async ValueTask OnShutdown(IEventActorContext<FuturesAnalyticsObservationRealtimeActor> runtime)
    {
        runtime.RemoveRealtimeRouter(MarketPriceRoute, Id);
        await barrierStopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await barrierLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (barrierStopping.IsCancellationRequested)
        {
        }
    }

    /// <summary>Parses routed price updates and self-published closed observations.</summary>
    protected override IEvent ParseMessage(
        IEventActorContext<FuturesAnalyticsObservationRealtimeActor> runtime,
        IActorMessage message) => message.Subject.Verb switch
        {
            FuturesMarketPriceUpdatedRealtimeEvent.Verb =>
                message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!,
            FuturesAnalyticsObservationClosedRealtimeEvent.Verb =>
                message.AsEvent<FuturesAnalyticsObservationClosedRealtimeEvent>()!,
            FuturesAnalyticsObservationBarrierRealtimeEvent.Verb =>
                message.AsEvent<FuturesAnalyticsObservationBarrierRealtimeEvent>()!,
            _ => default!
        };

    /// <summary>Aggregates normalized trades and projects each newly closed interval once.</summary>
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesAnalyticsObservationRealtimeActor> runtime,
        IEvent @event)
    {
        if (@event is FuturesAnalyticsObservationClosedRealtimeEvent) return;
        if (@event is FuturesAnalyticsObservationBarrierRealtimeEvent barrier)
        {
            foreach (var observation in state.CloseThrough(barrier.BarrierUtc, context.TimeProvider))
                await context.Projector.ProjectAsync(runtime, observation).ConfigureAwait(false);
            return;
        }
        if (@event is not FuturesMarketPriceUpdatedRealtimeEvent price)
            throw new InvalidOperationException($"Unsupported observation event {@event.EventName}.");
        var series = context.SeriesResolver.Resolve(price.Price.ContractId);
        foreach (var observation in state.Accept(price, series, context.Calendar, context.TimeProvider))
            await context.Projector.ProjectAsync(runtime, observation).ConfigureAwait(false);
    }

    static async Task RunBarrierLoopAsync(
        IEventActorContext<FuturesAnalyticsObservationRealtimeActor> runtime,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await runtime.SendAsync<FuturesAnalyticsObservationBarrierRealtimeEvent, ActorEntityId>(
                FuturesAnalyticsObservationBarrierRealtimeEvent.Create(
                    timeProvider.GetUtcNow())).ConfigureAwait(false);
        }
    }

    /// <summary>Reports actor failures through the standard realtime error event.</summary>
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesAnalyticsObservationRealtimeActor> runtime,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, runtime).ConfigureAwait(false);
}
