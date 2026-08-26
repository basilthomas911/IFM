using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
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
    /// <inheritdoc />
    protected override IEvent ParseMessage(IEventActorContext<FuturesBbSignalEventActor> context, IActorMessage message) =>
        message.Subject is { ActorType: ActorType.Event, Name: ActorName,
            Verb: FuturesBbSignalGeneratedCompleteEvent.Verb }
            ? message.AsEvent<FuturesBbSignalGeneratedCompleteEvent>()! : default!;
    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(IEventActorContext<FuturesBbSignalEventActor> context, IEvent @event) =>
        @event is FuturesBbSignalGeneratedCompleteEvent
            ? ValueTask.CompletedTask
            : throw new InvalidOperationException($"Unsupported Bollinger event {@event.EventName}.");
    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FuturesBbSignalEventActor> context,
        ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context);
}
