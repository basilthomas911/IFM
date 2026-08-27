using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Event.Actor;

/// <summary>Consumes projected VX lifecycle events without retaining state.</summary>
public sealed class FuturesVxTermStructureSignalEventActor(
    IEventActorContext<FuturesVxTermStructureSignalEventActor> actorContext)
    : BaseEventActor<FuturesVxTermStructureSignalEventActor>(actorContext,
        ((IFuturesVxTermStructureSignalEventContext)actorContext).Logger)
{
    /// <summary>Identifies the VX term-structure Event mailbox.</summary>
    public const string ActorName = FuturesVxTermStructureSignalUpdatedEvent.Actor;
    /// <summary>Gets the typed Event context supplied through open-generic registration.</summary>
    IFuturesVxTermStructureSignalEventContext TypedContext { get; } = IsArgumentNull.Set(
        actorContext as IFuturesVxTermStructureSignalEventContext, nameof(actorContext))!;
    readonly Dictionary<Type, Func<IEvent, IFuturesVxTermStructureSignalEventContext, ILogger, ValueTask<bool>>>
        receiveMap = new()
        {
            [typeof(FuturesVxTermStructureSignalUpdatedCompleteEvent)] = async (@event, context, eventLogger) =>
                await ((FuturesVxTermStructureSignalUpdatedCompleteEvent)@event)
                    .ExecuteAsync(context, eventLogger).ConfigureAwait(false),
            [typeof(FuturesVxTermStructureSignalUpdatedFailEvent)] = async (@event, context, eventLogger) =>
                await ((FuturesVxTermStructureSignalUpdatedFailEvent)@event)
                    .ExecuteAsync(context, eventLogger).ConfigureAwait(false)
        };
    /// <inheritdoc />
    protected override IEvent ParseMessage(IEventActorContext<FuturesVxTermStructureSignalEventActor> context,
        IActorMessage message) => message.Subject is { ActorType: ActorType.Event, Name: ActorName }
        ? message.Subject.Verb switch
        {
            FuturesVxTermStructureSignalUpdatedCompleteEvent.Verb =>
                message.AsEvent<FuturesVxTermStructureSignalUpdatedCompleteEvent>()!,
            FuturesVxTermStructureSignalUpdatedFailEvent.Verb =>
                message.AsEvent<FuturesVxTermStructureSignalUpdatedFailEvent>()!,
            _ => default!
        } : default!;
    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesVxTermStructureSignalEventActor> context,
        IEvent @event)
    {
        if (!receiveMap.TryGetValue(@event.GetType(), out var handler))
            throw new InvalidOperationException($"Unsupported VX Event actor message {@event.EventName}.");
        _ = await handler(@event, TypedContext, TypedContext.Logger).ConfigureAwait(false);
    }
    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesVxTermStructureSignalEventActor> context,
        ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context);
}
