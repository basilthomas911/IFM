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

    /// <inheritdoc />
    protected override IEvent ParseMessage(IEventActorContext<FuturesEmaSignalEventActor> context, IActorMessage message) =>
        message.Subject is { ActorType: ActorType.Event, Name: ActorName,
            Verb: FuturesEmaSignalGeneratedCompleteEvent.Verb }
            ? message.AsEvent<FuturesEmaSignalGeneratedCompleteEvent>()! : default!;
    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesEmaSignalEventActor> context, IEvent @event)
    {
        if (@event is not FuturesEmaSignalGeneratedCompleteEvent completed)
            throw new InvalidOperationException($"Unsupported EMA event {@event.EventName}.");
        _ = await completed.ExecuteAsync(typedContext, typedContext.Logger);
    }
    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FuturesEmaSignalEventActor> context,
        ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context);
}
