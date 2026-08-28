using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime.Actor;

/// <summary>Routes closed observations to the event-sourced ATR command actor without retaining realtime state.</summary>
/// <param name="actorContext">The typed ATR realtime context.</param>
public class FuturesAtrSignalRealtimeActor(IRealtimeActorContext<FuturesAtrSignalRealtimeActor> actorContext)
    : BaseEventActor<FuturesAtrSignalRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Identifies the ATR realtime mailbox.</summary>
    public const string ActorName = "FuturesAtrSignal";

    /// <summary>Gets the typed context supplied to this actor.</summary>
    protected IFuturesAtrSignalRealtimeContext FuturesAtrSignalRealtimeContext { get; } = IsArgumentNull.Set(
        actorContext as IFuturesAtrSignalRealtimeContext, nameof(actorContext))!;

    static readonly ActorTypeId ObservationRoute = new(
        ActorType.Realtime,
        FuturesTradeSessionBarClosedRealtimeEvent.Actor,
        FuturesTradeSessionBarClosedRealtimeEvent.Verb);
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesTradeSessionBarClosedRealtimeEvent.Verb] =
                message => message.AsEvent<FuturesTradeSessionBarClosedRealtimeEvent>()!
        };
    readonly ILogger<FuturesAtrSignalRealtimeActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesAtrSignalRealtimeContext, ILogger, ValueTask<bool>>> _receiveMap =
        new Dictionary<Type, Func<IEvent, IFuturesAtrSignalRealtimeContext, ILogger, ValueTask<bool>>>
    {
        [typeof(FuturesTradeSessionBarClosedRealtimeEvent)] = async (@event, context, logger) =>
            await ((FuturesTradeSessionBarClosedRealtimeEvent)@event)
                .ExecuteAsync(context, logger).ConfigureAwait(false)
    };

    /// <summary>Registers the shared observation route.</summary>
    protected override ValueTask OnStartup(IEventActorContext<FuturesAtrSignalRealtimeActor> context)
    {
        IsArgumentNull.Check(context);
        context.AddRealtimeRouter(ObservationRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <summary>Removes the shared observation route.</summary>
    protected override ValueTask OnShutdown(IEventActorContext<FuturesAtrSignalRealtimeActor> context)
    {
        IsArgumentNull.Check(context);
        context.RemoveRealtimeRouter(ObservationRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <summary>Parses a routed closed-observation event.</summary>
    protected override IEvent ParseMessage(IEventActorContext<FuturesAtrSignalRealtimeActor> context, IActorMessage message)
        => ParseMappedRealtimeEvent(context, message, _parseMap);

    /// <summary>Dispatches a routed event to its dedicated extension handler.</summary>
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesAtrSignalRealtimeActor> context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        _ = await handler(@event, FuturesAtrSignalRealtimeContext, _logger).ConfigureAwait(false);
    }

    /// <summary>Publishes the standard actor error event.</summary>
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesAtrSignalRealtimeActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) =>
        await exception.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
