using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Event.Actor;

/// <summary>Consumes successfully projected Bollinger lifecycle events without retaining state.</summary>
public sealed class FuturesBbSignalEventActor(IEventActorContext<FuturesBbSignalEventActor> actorContext)
    : BaseEventActor<FuturesBbSignalEventActor>(actorContext,
        ((IFuturesBbSignalEventContext)actorContext).Logger)
{
    /// <summary>Gets the event mailbox name.</summary>
    public const string ActorName = FuturesBbSignalGeneratedEvent.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesBbSignalGeneratedCompleteEvent.Verb] = static message =>
                message.AsEvent<FuturesBbSignalGeneratedCompleteEvent>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<IEvent, IEventActorContext<FuturesBbSignalEventActor>, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IEvent, IEventActorContext<FuturesBbSignalEventActor>, ValueTask>>
        {
            [typeof(FuturesBbSignalGeneratedCompleteEvent)] = static (@event, context) =>
                context.PublishMarketOutlookComponentAsync((FuturesBbSignalGeneratedCompleteEvent)@event)
        };

    /// <inheritdoc />
    protected override IEvent ParseMessage(IEventActorContext<FuturesBbSignalEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);
    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(IEventActorContext<FuturesBbSignalEventActor> context, IEvent @event)
        => ResolveMappedEventHandler(@event, _receiveMap)(@event, context);
    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FuturesBbSignalEventActor> context,
        ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context);
}
