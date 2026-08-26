using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Actor;

/// <summary>Routes closed observations to the event-sourced RSI command actor without retaining realtime state.</summary>
/// <param name="actorContext">The typed RSI realtime context.</param>
public class FuturesRsiSignalRealtimeActor(IRealtimeActorContext<FuturesRsiSignalRealtimeActor> actorContext)
    : BaseEventActor<FuturesRsiSignalRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Identifies the RSI realtime mailbox.</summary>
    public const string ActorName = "FuturesRsiSignal";
    /// <summary>Gets the typed context supplied to this actor.</summary>
    protected IFuturesRsiSignalRealtimeContext FuturesRsiSignalRealtimeContext { get; } = IsArgumentNull.Set(
        actorContext as IFuturesRsiSignalRealtimeContext, nameof(actorContext))!;

    static readonly ActorTypeId ObservationRoute = new(ActorType.Realtime,
        FuturesTradeSessionBarClosedRealtimeEvent.Actor,
        FuturesTradeSessionBarClosedRealtimeEvent.Verb);
    readonly ILogger<FuturesRsiSignalRealtimeActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    readonly Dictionary<Type, Func<IEvent, IFuturesRsiSignalRealtimeContext, ILogger, ValueTask<bool>>> _receiveMap = new()
    {
        [typeof(FuturesTradeSessionBarClosedRealtimeEvent)] = async (@event, context, logger) =>
            await ((FuturesTradeSessionBarClosedRealtimeEvent)@event).ExecuteAsync(context, logger).ConfigureAwait(false)
    };

    /// <summary>Registers the shared observation route.</summary>
    protected override ValueTask OnStartup(IEventActorContext<FuturesRsiSignalRealtimeActor> context)
    {
        context.AddRealtimeRouter(ObservationRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <summary>Removes the shared observation route.</summary>
    protected override ValueTask OnShutdown(IEventActorContext<FuturesRsiSignalRealtimeActor> context)
    {
        context.RemoveRealtimeRouter(ObservationRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <summary>Parses a routed closed-observation event.</summary>
    protected override IEvent ParseMessage(IEventActorContext<FuturesRsiSignalRealtimeActor> context, IActorMessage message) =>
        message.Subject.Is(ActorType.Realtime, ActorName, FuturesTradeSessionBarClosedRealtimeEvent.Verb)
            ? message.AsEvent<FuturesTradeSessionBarClosedRealtimeEvent>()!
            : default!;

    /// <summary>Dispatches a routed event to its dedicated extension handler.</summary>
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesRsiSignalRealtimeActor> context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        if (!_receiveMap.TryGetValue(@event.GetType(), out var handler))
            throw new InvalidOperationException($"Unable to resolve {ActorName} realtime event from message: {@event.Subject}");
        _ = await handler(@event, FuturesRsiSignalRealtimeContext, _logger).ConfigureAwait(false);
    }

    /// <summary>Publishes the standard actor error event.</summary>
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FuturesRsiSignalRealtimeActor> context,
        ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
