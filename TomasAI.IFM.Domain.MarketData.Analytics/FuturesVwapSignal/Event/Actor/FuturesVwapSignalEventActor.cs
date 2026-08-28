using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Event.Actor;

/// <summary>Statelessly dispatches VWAP projection lifecycle events.</summary>
public sealed class FuturesVwapSignalEventActor(
    IEventActorContext<FuturesVwapSignalEventActor> actorContext)
    : BaseEventActor<FuturesVwapSignalEventActor>(actorContext,
        ((IFuturesVwapSignalEventContext)actorContext).Logger)
{
    /// <summary>Identifies the VWAP Event mailbox.</summary>
    public const string ActorName = FuturesVwapSignalUpdatedEvent.Actor;
    IFuturesVwapSignalEventContext TypedContext { get; } = IsArgumentNull.Set(
        actorContext as IFuturesVwapSignalEventContext, nameof(actorContext))!;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesVwapSignalUpdatedCompleteEvent.Verb] = static message =>
                message.AsEvent<FuturesVwapSignalUpdatedCompleteEvent>()!,
            [FuturesVwapSignalUpdatedFailEvent.Verb] = static message =>
                message.AsEvent<FuturesVwapSignalUpdatedFailEvent>()!
        };
    readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesVwapSignalEventContext, ILogger, ValueTask<bool>>>
        _receiveMap = new Dictionary<Type, Func<IEvent, IFuturesVwapSignalEventContext, ILogger, ValueTask<bool>>>()
        {
            [typeof(FuturesVwapSignalUpdatedCompleteEvent)] = async (@event, context, logger) =>
                await ((FuturesVwapSignalUpdatedCompleteEvent)@event)
                    .ExecuteAsync(context, logger).ConfigureAwait(false),
            [typeof(FuturesVwapSignalUpdatedFailEvent)] = async (@event, context, logger) =>
                await ((FuturesVwapSignalUpdatedFailEvent)@event)
                    .ExecuteAsync(context, logger).ConfigureAwait(false)
        };

    /// <inheritdoc />
    protected override IEvent ParseMessage(IEventActorContext<FuturesVwapSignalEventActor> context,
        IActorMessage message) => ParseMappedEvent(context, message, _parseMap);

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesVwapSignalEventActor> context, IEvent @event)
    {
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        _ = await handler(@event, TypedContext, TypedContext.Logger).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesVwapSignalEventActor> context,
        ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context);
}
