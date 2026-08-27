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
    readonly Dictionary<Type, Func<IEvent, IFuturesVwapSignalEventContext, ILogger, ValueTask<bool>>>
        receiveMap = new()
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
        IActorMessage message) => message.Subject is { ActorType: ActorType.Event, Name: ActorName }
        ? message.Subject.Verb switch
        {
            FuturesVwapSignalUpdatedCompleteEvent.Verb =>
                message.AsEvent<FuturesVwapSignalUpdatedCompleteEvent>()!,
            FuturesVwapSignalUpdatedFailEvent.Verb =>
                message.AsEvent<FuturesVwapSignalUpdatedFailEvent>()!,
            _ => default!
        } : default!;

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesVwapSignalEventActor> context, IEvent @event)
    {
        if (!receiveMap.TryGetValue(@event.GetType(), out var handler))
            throw new InvalidOperationException($"Unsupported VWAP Event actor message {@event.EventName}.");
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
