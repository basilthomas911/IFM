using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Actor;

/// <summary>Routes closed observations to the event-sourced MACD command actor without retaining realtime state.</summary>
/// <param name="actorContext">The typed MACD realtime context.</param>
public class FuturesMacdSignalRealtimeActor(IRealtimeActorContext<FuturesMacdSignalRealtimeActor> actorContext)
    : BaseEventActor<FuturesMacdSignalRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Identifies the MACD realtime mailbox.</summary>
    public const string ActorName = "FuturesMacdSignal";
    /// <summary>Gets the typed context supplied to this actor.</summary>
    protected IFuturesMacdSignalRealtimeContext FuturesMacdSignalRealtimeContext { get; } = IsArgumentNull.Set(
        actorContext as IFuturesMacdSignalRealtimeContext, nameof(actorContext))!;

    static readonly ActorTypeId ObservationRoute = new(ActorType.Realtime,
        FuturesTradeSessionBarClosedRealtimeEvent.Actor,
        FuturesTradeSessionBarClosedRealtimeEvent.Verb);
    readonly ILogger<FuturesMacdSignalRealtimeActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    readonly Dictionary<Type, Func<IEvent, IFuturesMacdSignalRealtimeContext, ILogger, ValueTask<bool>>> _receiveMap = new()
    {
        [typeof(FuturesTradeSessionBarClosedRealtimeEvent)] = async (@event, context, logger) =>
            await ((FuturesTradeSessionBarClosedRealtimeEvent)@event).ExecuteAsync(context, logger).ConfigureAwait(false)
    };

    /// <summary>Registers the shared observation route.</summary>
    protected override ValueTask OnStartup(IEventActorContext<FuturesMacdSignalRealtimeActor> context)
    {
        context.AddRealtimeRouter(ObservationRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <summary>Removes the shared observation route.</summary>
    protected override ValueTask OnShutdown(IEventActorContext<FuturesMacdSignalRealtimeActor> context)
    {
        context.RemoveRealtimeRouter(ObservationRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <summary>Parses a routed closed-observation event.</summary>
    protected override IEvent ParseMessage(IEventActorContext<FuturesMacdSignalRealtimeActor> context, IActorMessage message) =>
        message.Subject.Is(ActorType.Realtime, ActorName, FuturesTradeSessionBarClosedRealtimeEvent.Verb)
            ? message.AsEvent<FuturesTradeSessionBarClosedRealtimeEvent>()!
            : default!;

    /// <summary>Dispatches a routed event to its dedicated extension handler.</summary>
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesMacdSignalRealtimeActor> context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        if (!_receiveMap.TryGetValue(@event.GetType(), out var handler))
            throw new InvalidOperationException($"Unable to resolve {ActorName} realtime event from message: {@event.Subject}");
        _ = await handler(@event, FuturesMacdSignalRealtimeContext, _logger).ConfigureAwait(false);
    }

    /// <summary>Publishes the standard actor error event.</summary>
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FuturesMacdSignalRealtimeActor> context,
        ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
