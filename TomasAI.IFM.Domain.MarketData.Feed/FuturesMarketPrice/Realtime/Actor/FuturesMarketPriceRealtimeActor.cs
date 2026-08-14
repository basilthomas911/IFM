using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesMarketPrice.Realtime.Actor;

/// <summary>
/// Serves as the required primary actor destination for futures market-price realtime events.
/// </summary>
/// <param name="supervisor">The supervisor that owns the actor mailbox and messaging resources.</param>
/// <param name="logger">The typed logger used by the actor and its event handlers.</param>
public class FuturesMarketPriceRealtimeActor(
    IActorSupervisor supervisor,
    ILogger<FuturesMarketPriceRealtimeActor> logger)
    : BaseEventActor<FuturesMarketPriceRealtimeActor>(
        supervisor,
        logger,
        new ActorMailboxId(ActorType.Realtime, ActorName))
{
    /// <summary>Identifies the primary futures market-price realtime actor.</summary>
    public const string ActorName = FuturesMarketPriceUpdatedRealtimeEvent.Actor;

    /// <summary>Maps supported realtime verbs to their concrete MessagePack deserializers.</summary>
    static readonly Dictionary<string, Func<IActorMessage, IEvent>> _parseMap = new()
    {
        [FuturesMarketPriceUpdatedRealtimeEvent.Verb] =
            message => message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!
    };

    /// <summary>Maps supported realtime event types to their extension handlers.</summary>
    readonly Dictionary<string, Func<IEvent, IEventActorContext, ValueTask<bool>>> _receiveMap = new()
    {
        [typeof(FuturesMarketPriceUpdatedRealtimeEvent).Name] =
            (@event, context) => ((FuturesMarketPriceUpdatedRealtimeEvent)@event)
                .ExecuteAsync(context, logger)
    };

    /// <summary>
    /// Parses a supported futures market-price realtime event from an actor message.
    /// </summary>
    /// <param name="context">The event actor context processing the message.</param>
    /// <param name="message">The actor message containing the serialized realtime event.</param>
    /// <returns>The parsed event, or <see langword="null"/> when the subject is not supported.</returns>
    protected override IEvent ParseMessage(IEventActorContext context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(message);

        var subject = message.Subject;
        if (subject is not { ActorType: ActorType.Realtime, Name: ActorName }
            || !_parseMap.TryGetValue(subject.Verb, out var messageParser))
            return default!;

        var @event = messageParser.Invoke(message);
        IsArgumentNull.Check(@event);
        @event.CheckForEmptyCommandId();
        return @event;
    }

    /// <summary>
    /// Dispatches a futures market-price realtime event to its extension handler.
    /// </summary>
    /// <param name="context">The event actor context supplied to the handler.</param>
    /// <param name="event">The realtime event to dispatch.</param>
    /// <returns>A task representing handler execution.</returns>
    protected override async ValueTask ReceiveAsync(IEventActorContext context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);

        if (!_receiveMap.TryGetValue(@event.GetType().Name, out var receiveHandler))
        {
            throw new InvalidOperationException(
                $"Unable to resolve {ActorName} realtime event from message: {@event.Subject}");
        }

        _ = await receiveHandler.Invoke(@event, context).ConfigureAwait(false);
    }

    /// <summary>
    /// Publishes the framework event-error notification for a realtime handler failure.
    /// </summary>
    /// <param name="context">The actor context used to publish the error event.</param>
    /// <param name="threadId">The actor thread on which processing failed.</param>
    /// <param name="event">The event being processed when the exception occurred.</param>
    /// <param name="exception">The handler exception.</param>
    /// <returns>A task representing error-event publication.</returns>
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
