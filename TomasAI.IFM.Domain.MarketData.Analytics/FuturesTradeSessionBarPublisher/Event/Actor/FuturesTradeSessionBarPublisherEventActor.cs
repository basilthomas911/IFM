using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Event.Actor;

/// <summary>Dispatches durable trade-session bar publication events without retaining domain state.</summary>
public sealed class FuturesTradeSessionBarPublisherEventActor(
    IEventActorContext<FuturesTradeSessionBarPublisherEventActor> actorContext)
    : BaseEventActor<FuturesTradeSessionBarPublisherEventActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the Event actor mailbox name.</summary>
    public const string ActorName = FuturesTradeSessionBarPublishedEvent.Actor;

    readonly IFuturesTradeSessionBarPublisherEventContext context = IsArgumentNull.Set(
        actorContext as IFuturesTradeSessionBarPublisherEventContext,
        nameof(actorContext))!;
    readonly ILogger<FuturesTradeSessionBarPublisherEventActor> logger = IsArgumentNull.Set(actorContext.Logger);

    static readonly Dictionary<string, Func<IActorMessage, IEvent>> ParseMap = new()
    {
        [FuturesTradeSessionBarPublishedEvent.Verb] =
            message => message.AsEvent<FuturesTradeSessionBarPublishedEvent>()!,
        [FuturesTradeSessionBarPublishedCompleteEvent.Verb] =
            message => message.AsEvent<FuturesTradeSessionBarPublishedCompleteEvent>()!,
        [FuturesTradeSessionBarPublishedFailEvent.Verb] =
            message => message.AsEvent<FuturesTradeSessionBarPublishedFailEvent>()!
    };

    readonly Dictionary<Type, Func<IEvent, IFuturesTradeSessionBarPublisherEventContext, ILogger, ValueTask<bool>>>
        receiveMap = new()
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
        IEventActorContext<FuturesTradeSessionBarPublisherEventActor> actorContext,
        IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Event, Name: ActorName }
            || !ParseMap.TryGetValue(message.Subject.Verb, out var parser))
            return default!;
        return parser(message);
    }

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesTradeSessionBarPublisherEventActor> actorContext,
        IEvent @event)
    {
        if (!receiveMap.TryGetValue(@event.GetType(), out var handler))
            throw new InvalidOperationException($"Unable to resolve {ActorName} event from {@event.Subject}.");
        _ = await handler(@event, context, logger).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesTradeSessionBarPublisherEventActor> actorContext,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) => await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, actorContext).ConfigureAwait(false);
}
