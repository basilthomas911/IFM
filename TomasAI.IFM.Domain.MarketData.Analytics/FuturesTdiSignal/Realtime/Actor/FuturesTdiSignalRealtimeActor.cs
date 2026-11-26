using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;

using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;

/// <summary>Provides the FuturesTdiSignalRealtimeActor implementation.</summary>
public class FuturesTdiSignalRealtimeActor(
    IRealtimeActorContext<FuturesTdiSignalRealtimeActor> actorContext)
    : BaseEventActor<FuturesTdiSignalRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IFuturesTdiSignalRealtimeContext ActorContext { get; } =
        IsArgumentNull.Named(actorContext as IFuturesTdiSignalRealtimeContext, nameof(actorContext))!;

    public const string ActorName = "FuturesTdiSignal";
    readonly FuturesTdiSignalRealtimeState _state = new((actorContext as IFuturesTdiSignalRealtimeContext)?.Receipts);
    CancellationTokenSource? recovering;
    Task recoveryLoop = Task.CompletedTask;
    /// <inheritdoc/>
    protected override bool PropagateHandlerFailures => true;
    static readonly ActorTypeId RsiSignalsRoute = new(
        ActorType.Realtime,
        FuturesRsiSignalRealtimeActor.ActorName,
        FuturesRsiSignalsGeneratedEvent.Verb);
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesRsiSignalsGeneratedEvent.Verb] = message => message.AsEvent<FuturesRsiSignalsGeneratedEvent>()!,
            [FuturesTdiSignalGeneratedEvent.Verb] = message => message.AsEvent<FuturesTdiSignalGeneratedEvent>()!,
            [FuturesTdiSignalGeneratedCompleteEvent.Verb] = message => message.AsEvent<FuturesTdiSignalGeneratedCompleteEvent>()!,
            [FuturesTdiSignalGeneratedFailEvent.Verb] = message => message.AsEvent<FuturesTdiSignalGeneratedFailEvent>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<IEvent,
        IEventActorContext<FuturesTdiSignalRealtimeActor>,
        IFuturesTdiSignalRealtimeContext,
        FuturesTdiSignalRealtimeState,
        ValueTask<bool>>> _receiveMap =
        new Dictionary<Type, Func<IEvent,
            IEventActorContext<FuturesTdiSignalRealtimeActor>,
            IFuturesTdiSignalRealtimeContext,
            FuturesTdiSignalRealtimeState,
            ValueTask<bool>>>
        {
            [typeof(FuturesRsiSignalsGeneratedEvent)] = static (@event, eventContext, context, state) =>
                ((FuturesRsiSignalsGeneratedEvent)@event).ExecuteAsync(
                    context.Projector, context.DbFactory.MarketDataDb, state, context.Logger),
            [typeof(FuturesTdiSignalGeneratedFailEvent)] = static (@event, eventContext, context, state) =>
                ((FuturesTdiSignalGeneratedFailEvent)@event).ExecuteAsync(context),
            [typeof(FuturesTdiSignalGeneratedCompleteEvent)] = static (@event, eventContext, context, state) =>
                ((FuturesTdiSignalGeneratedCompleteEvent)@event).ExecuteAsync(eventContext, context),
            [typeof(FuturesTdiSignalGeneratedEvent)] = static (@event, eventContext, context, state) =>
                ((FuturesTdiSignalGeneratedEvent)@event).ExecuteAsync(context)
        };

    protected override async ValueTask OnStartup(IEventActorContext<FuturesTdiSignalRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await actorContext.Projector.StartAsync(context).ConfigureAwait(false);
        context.AddRealtimeRouter(RsiSignalsRoute, Id);
        recovering = new();
        recoveryLoop = RecoverPendingAsync(recovering.Token);
    }

    protected override async ValueTask OnShutdown(IEventActorContext<FuturesTdiSignalRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.RemoveRealtimeRouter(RsiSignalsRoute, Id);
        if (recovering is { } stopping)
        {
            await stopping.CancelAsync().ConfigureAwait(false);
            try { await recoveryLoop.ConfigureAwait(false); }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
            finally { stopping.Dispose(); recovering = null; recoveryLoop = Task.CompletedTask; }
        }
        await actorContext.Projector.StopAsync().ConfigureAwait(false);
    }

    protected override IEvent ParseMessage(IEventActorContext<FuturesTdiSignalRealtimeActor> context, IActorMessage message)
        => ParseMappedRealtimeEvent(context, message, _parseMap);

    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesTdiSignalRealtimeActor> context, IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        if (!await handler(@event, context, ActorContext, _state).ConfigureAwait(false))
            throw new IOException("TDI result/publication is incomplete; input must not be acknowledged.");
    }

    async Task RecoverPendingAsync(CancellationToken token)
    {
        await Task.Yield();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            if (!IsRunning) continue;
            try
            {
                using var generation = RealtimeGeneration.Enter();
                if (!await _state.RecoverAsync(ActorContext.Projector, token).ConfigureAwait(false))
                    ActorContext.Logger.LogError("TDI result recovery remains pending.");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested || RealtimeGeneration.Token.IsCancellationRequested) { return; }
            catch (Exception error) { ActorContext.Logger.LogError(error, "TDI durable result recovery failed; pending receipts retained."); }
        }
    }

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesTdiSignalRealtimeActor> context, ActorThreadId threadId, IEvent @event, Exception ex) =>
        await ex.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
