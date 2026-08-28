using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesMarketPrice.Realtime.Extensions;
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
/// <param name="actorContext.Logger">The typed actorContext.Logger used by the actor and its event handlers.</param>
public class FuturesMarketPriceRealtimeActor(IRealtimeActorContext<FuturesMarketPriceRealtimeActor> actorContext)
    : BaseEventActor<FuturesMarketPriceRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Identifies the primary futures market-price realtime actor.</summary>
    public const string ActorName = FuturesMarketPriceUpdatedRealtimeEvent.Actor;

    /// <summary>Gets the typed realtime context supplied at construction.</summary>
    protected IFuturesMarketPriceRealtimeContext RealtimeContext { get; } = IsArgumentNull.Set(actorContext as IFuturesMarketPriceRealtimeContext, nameof(actorContext))!;

    /// <summary>Maps supported realtime verbs to their concrete MessagePack deserializers.</summary>
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
    {
        [FuturesMarketPriceUpdatedRealtimeEvent.Verb] =
            message => message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!
    };

    /// <summary>Maps supported realtime event types to their extension handlers.</summary>
    readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesMarketPriceRealtimeContext, ValueTask<bool>>> _receiveMap =
        new Dictionary<Type, Func<IEvent, IFuturesMarketPriceRealtimeContext, ValueTask<bool>>>
    {
        [typeof(FuturesMarketPriceUpdatedRealtimeEvent)] =
            (@event, context) => ((FuturesMarketPriceUpdatedRealtimeEvent)@event)
                .ExecuteAsync(context, actorContext.Logger)
    };

    /// <summary>
    /// Parses a supported futures market-price realtime event from an actor message.
    /// </summary>
    /// <param name="context">The event actor context processing the message.</param>
    /// <param name="message">The actor message containing the serialized realtime event.</param>
    /// <returns>The parsed event, or <see langword="null"/> when the subject is not supported.</returns>
    protected override IEvent ParseMessage(IEventActorContext<FuturesMarketPriceRealtimeActor> context, IActorMessage message)
        => ParseMappedRealtimeEvent(context, message, _parseMap);

    /// <summary>
    /// Dispatches a futures market-price realtime event to its extension handler.
    /// </summary>
    /// <param name="context">The event actor context supplied to the handler.</param>
    /// <param name="event">The realtime event to dispatch.</param>
    /// <returns>A task representing handler execution.</returns>
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesMarketPriceRealtimeActor> context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);

        var receiveHandler = ResolveMappedEventHandler(@event, _receiveMap);
        _ = await receiveHandler.Invoke(@event, RealtimeContext).ConfigureAwait(false);
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
        IEventActorContext<FuturesMarketPriceRealtimeActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
