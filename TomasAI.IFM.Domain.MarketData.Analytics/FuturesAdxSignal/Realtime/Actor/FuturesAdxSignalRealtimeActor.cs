using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Actor;

/// <summary>
/// Receives routed closed analytics observations and dispatches them to the ADX command-forwarding handler.
/// This actor retains no domain state and performs no projection.
/// </summary>
/// <param name="actorContext">The typed ADX realtime context resolved through open-generic registration.</param>
public class FuturesAdxSignalRealtimeActor(
    IRealtimeActorContext<FuturesAdxSignalRealtimeActor> actorContext)
    : BaseEventActor<FuturesAdxSignalRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Identifies the ADX realtime actor mailbox.</summary>
    public const string ActorName = "FuturesAdxSignal";

    /// <summary>Gets the typed realtime context supplied when this actor is constructed.</summary>
    protected IFuturesAdxSignalRealtimeContext FuturesAdxSignalRealtimeContext { get; } = IsArgumentNull.Set(
        actorContext as IFuturesAdxSignalRealtimeContext,
        nameof(actorContext))!;

    static readonly ActorTypeId ObservationRoute = new(
        ActorType.Realtime,
        FuturesTradeSessionBarClosedRealtimeEvent.Actor,
        FuturesTradeSessionBarClosedRealtimeEvent.Verb);

    readonly ILogger<FuturesAdxSignalRealtimeActor> _logger = IsArgumentNull.Set(actorContext.Logger);

    /// <summary>Maps supported realtime event types to their dedicated extension handlers.</summary>
    readonly Dictionary<Type, Func<IEvent, IFuturesAdxSignalRealtimeContext, ILogger, ValueTask<bool>>> _receiveMap = new()
    {
        [typeof(FuturesTradeSessionBarClosedRealtimeEvent)] = async (@event, context, logger) =>
        {
            var closed = (@event as FuturesTradeSessionBarClosedRealtimeEvent)!;
            return await closed.ExecuteAsync(context, logger).ConfigureAwait(false);
        }
    };

    /// <summary>Registers the shared closed-observation route.</summary>
    protected override ValueTask OnStartup(IEventActorContext<FuturesAdxSignalRealtimeActor> context)
    {
        IsArgumentNull.Check(context);
        context.AddRealtimeRouter(ObservationRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <summary>Removes the shared closed-observation route.</summary>
    protected override ValueTask OnShutdown(IEventActorContext<FuturesAdxSignalRealtimeActor> context)
    {
        IsArgumentNull.Check(context);
        context.RemoveRealtimeRouter(ObservationRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <summary>Parses a routed shared closed-observation event.</summary>
    protected override IEvent ParseMessage(
        IEventActorContext<FuturesAdxSignalRealtimeActor> context,
        IActorMessage message)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(message);
        var subject = message.Subject;
        return subject.Is(
            ActorType.Realtime,
            ActorName,
            FuturesTradeSessionBarClosedRealtimeEvent.Verb)
                ? message.AsEvent<FuturesTradeSessionBarClosedRealtimeEvent>()!
                : default!;
    }

    /// <summary>Dispatches a closed observation to its ADX command-forwarding extension handler.</summary>
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesAdxSignalRealtimeActor> context,
        IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        if (!_receiveMap.TryGetValue(@event.GetType(), out var receiveHandler))
            throw new InvalidOperationException(
                $"Unable to resolve {ActorName} realtime event from message: {@event.Subject}");
        _ = await receiveHandler
            .Invoke(@event, FuturesAdxSignalRealtimeContext, _logger)
            .ConfigureAwait(false);
    }

    /// <summary>Publishes the standard actor error event for an unhandled realtime exception.</summary>
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesAdxSignalRealtimeActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
