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
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesVxTermStructureSignalUpdatedCompleteEvent.Verb] = static message =>
                message.AsEvent<FuturesVxTermStructureSignalUpdatedCompleteEvent>()!,
            [FuturesVxTermStructureSignalUpdatedFailEvent.Verb] = static message =>
                message.AsEvent<FuturesVxTermStructureSignalUpdatedFailEvent>()!
        };
    readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesVxTermStructureSignalEventContext, ILogger, ValueTask<bool>>>
        _receiveMap = new Dictionary<Type, Func<IEvent, IFuturesVxTermStructureSignalEventContext, ILogger, ValueTask<bool>>>()
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
        IActorMessage message) => ParseMappedEvent(context, message, _parseMap);
    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesVxTermStructureSignalEventActor> context,
        IEvent @event)
    {
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
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
