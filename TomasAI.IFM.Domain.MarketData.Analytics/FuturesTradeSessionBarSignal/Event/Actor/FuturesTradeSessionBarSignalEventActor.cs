using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Event.Actor;

/// <summary>Dispatches durable trade-session bar publication events without retaining domain state.</summary>
public sealed class FuturesTradeSessionBarSignalEventActor(
    IEventActorContext<FuturesTradeSessionBarSignalEventActor> actorContext)
    : BaseEventActor<FuturesTradeSessionBarSignalEventActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the Event actor mailbox name.</summary>
    public const string ActorName = FuturesTradeSessionBarPublishedEvent.Actor;

    readonly IFuturesTradeSessionBarSignalEventContext context = IsArgumentNull.Set(
        actorContext as IFuturesTradeSessionBarSignalEventContext,
        nameof(actorContext))!;
    readonly ILogger<FuturesTradeSessionBarSignalEventActor> logger = IsArgumentNull.Set(actorContext.Logger);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>()
    {
        [FuturesTradeSessionBarPublishedEvent.Verb] =
            message => message.AsEvent<FuturesTradeSessionBarPublishedEvent>()!,
        [FuturesTradeSessionBarPublishedCompleteEvent.Verb] =
            message => message.AsEvent<FuturesTradeSessionBarPublishedCompleteEvent>()!,
        [FuturesTradeSessionBarPublishedFailEvent.Verb] =
            message => message.AsEvent<FuturesTradeSessionBarPublishedFailEvent>()!
    };

    readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesTradeSessionBarSignalEventContext, ILogger, ValueTask<bool>>>
        _receiveMap = new Dictionary<Type, Func<IEvent, IFuturesTradeSessionBarSignalEventContext, ILogger, ValueTask<bool>>>()
        {
            [typeof(FuturesTradeSessionBarPublishedEvent)] = static (@event, context, logger) =>
                ((FuturesTradeSessionBarPublishedEvent)@event).ExecuteAsync(context, logger),
            [typeof(FuturesTradeSessionBarPublishedCompleteEvent)] = static (@event, context, logger) =>
                ((FuturesTradeSessionBarPublishedCompleteEvent)@event).ExecuteAsync(context, logger),
            [typeof(FuturesTradeSessionBarPublishedFailEvent)] = static (@event, context, logger) =>
                ((FuturesTradeSessionBarPublishedFailEvent)@event).ExecuteAsync(context, logger)
        };

    /// <inheritdoc />
    protected override IEvent ParseMessage(
        IEventActorContext<FuturesTradeSessionBarSignalEventActor> actorContext,
        IActorMessage message)
        => ParseMappedEvent(actorContext, message, _parseMap);

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesTradeSessionBarSignalEventActor> actorContext,
        IEvent @event)
    {
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        _ = await handler(@event, context, logger).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesTradeSessionBarSignalEventActor> actorContext,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) => await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, actorContext).ConfigureAwait(false);
}
