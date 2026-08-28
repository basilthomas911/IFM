using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Event.Actor;

/// <summary>Processes projected EMA lifecycle events and starts downstream Bollinger work.</summary>
public sealed class FuturesEmaSignalEventActor(IEventActorContext<FuturesEmaSignalEventActor> actorContext)
    : BaseEventActor<FuturesEmaSignalEventActor>(actorContext,
        ((IFuturesEmaSignalEventContext)actorContext).Logger)
{
    /// <summary>Gets the event mailbox name.</summary>
    public const string ActorName = FuturesEmaSignalGeneratedEvent.Actor;
    readonly IFuturesEmaSignalEventContext typedContext = IsArgumentNull.Set(
        actorContext as IFuturesEmaSignalEventContext, nameof(actorContext))!;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesEmaSignalGeneratedCompleteEvent.Verb] = static message =>
                message.AsEvent<FuturesEmaSignalGeneratedCompleteEvent>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesEmaSignalEventContext, ValueTask<bool>>>
        _receiveMap = new Dictionary<Type, Func<IEvent, IFuturesEmaSignalEventContext, ValueTask<bool>>>
        {
            [typeof(FuturesEmaSignalGeneratedCompleteEvent)] = static (@event, context) =>
                ((FuturesEmaSignalGeneratedCompleteEvent)@event).ExecuteAsync(context, context.Logger)
        };

    /// <inheritdoc />
    protected override IEvent ParseMessage(IEventActorContext<FuturesEmaSignalEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);
    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesEmaSignalEventActor> context, IEvent @event)
    {
        var receive = ResolveMappedEventHandler(@event, _receiveMap);
        _ = await receive(@event, typedContext).ConfigureAwait(false);
    }
    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FuturesEmaSignalEventActor> context,
        ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context);
}
