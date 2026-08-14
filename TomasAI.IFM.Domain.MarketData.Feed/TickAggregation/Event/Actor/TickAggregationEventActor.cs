using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Event;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Event.Actor;

/// <summary>
/// Parses and dispatches tick-aggregation events to their event-family extension handlers.
/// </summary>
public sealed class TickAggregationEventActor(
    IActorSupervisor supervisor,
    IDbContextFactory dbFactory,
    ILogger<TickAggregationEventActor> logger)
    : BaseEventActor<TickAggregationEventActor>(
        supervisor, logger, new ActorMailboxId(ActorType.Event, ActorName))
{
    /// <summary>
    /// Identifies the Tick Aggregation event actor in actor message subjects.
    /// </summary>
    public const string ActorName = "TickAggregationEvent";

    /// <summary>
    /// Maps event verbs to their concrete message deserializers.
    /// </summary>
    static readonly Dictionary<string, Func<IActorMessage, IEvent>> _parseMap = new()
    {
        [FuturesTickTradeDataChangedEvent.Verb] =
            message => message.AsEvent<FuturesTickTradeDataChangedEvent>()!,
        [FuturesTickQuoteDataChangedEvent.Verb] =
            message => message.AsEvent<FuturesTickQuoteDataChangedEvent>()!,
        [FuturesTickTradeDataInsertedEvent.Verb] =
            message => message.AsEvent<FuturesTickTradeDataInsertedEvent>()!,
        [FuturesTickQuoteDataInsertedEvent.Verb] =
            message => message.AsEvent<FuturesTickQuoteDataInsertedEvent>()!,
        [FuturesTickTradeDataInsertedCompleteEvent.Verb] =
            message => message.AsEvent<FuturesTickTradeDataInsertedCompleteEvent>()!,
        [FuturesTickQuoteDataInsertedCompleteEvent.Verb] =
            message => message.AsEvent<FuturesTickQuoteDataInsertedCompleteEvent>()!,
        [FuturesTickTradeDataInsertedFailEvent.Verb] =
            message => message.AsEvent<FuturesTickTradeDataInsertedFailEvent>()!,
        [FuturesTickQuoteDataInsertedFailEvent.Verb] =
            message => message.AsEvent<FuturesTickQuoteDataInsertedFailEvent>()!
    };

    /// <summary>
    /// Maps concrete event types to the extension handler for the corresponding event family.
    /// </summary>
    readonly Dictionary<string, Func<IEvent, IEventActorContext, ValueTask<bool>>> _receiveMap = new()
    {
        [typeof(FuturesTickTradeDataChangedEvent).Name] =
            (@event, context) => ((FuturesTickTradeDataChangedEvent)@event).ExecuteAsync(context, logger),
        [typeof(FuturesTickQuoteDataChangedEvent).Name] =
            (@event, context) => ((FuturesTickQuoteDataChangedEvent)@event).ExecuteAsync(context, logger),
        [typeof(FuturesTickTradeDataInsertedEvent).Name] =
            (@event, context) => ((FuturesTickTradeDataInsertedEvent)@event).ExecuteAsync(context, dbFactory, logger),
        [typeof(FuturesTickQuoteDataInsertedEvent).Name] =
            (@event, context) => ((FuturesTickQuoteDataInsertedEvent)@event).ExecuteAsync(context, dbFactory, logger),
        [typeof(FuturesTickTradeDataInsertedCompleteEvent).Name] =
            (@event, context) => ((FuturesTickTradeDataInsertedCompleteEvent)@event).ExecuteAsync(context, logger),
        [typeof(FuturesTickQuoteDataInsertedCompleteEvent).Name] =
            (@event, context) => ((FuturesTickQuoteDataInsertedCompleteEvent)@event).ExecuteAsync(context, logger),
        [typeof(FuturesTickTradeDataInsertedFailEvent).Name] =
            (@event, context) => ((FuturesTickTradeDataInsertedFailEvent)@event).ExecuteAsync(context, logger),
        [typeof(FuturesTickQuoteDataInsertedFailEvent).Name] =
            (@event, context) => ((FuturesTickQuoteDataInsertedFailEvent)@event).ExecuteAsync(context, logger)
    };

    /// <summary>
    /// Parses a supported Tick Aggregation event from an actor message by resolving its subject verb.
    /// </summary>
    /// <param name="context">The event actor context processing the message.</param>
    /// <param name="message">The actor message containing the serialized event.</param>
    /// <returns>
    /// The parsed concrete event, or <see langword="null"/> when the subject does not target this actor
    /// or its verb is not supported.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="context"/> or <paramref name="message"/> is <see langword="null"/>.
    /// </exception>
    protected override IEvent ParseMessage(IEventActorContext context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(message);

        var subject = message.Subject;
        if (subject is not { ActorType: ActorType.Event, Name: ActorName }
            || !_parseMap.TryGetValue(subject.Verb, out var messageParser))
            return default!;

        var @event = messageParser.Invoke(message);
        IsArgumentNull.Check(@event);
        @event.CheckForEmptyCommandId();
        return @event;
    }

    /// <summary>
    /// Dispatches a concrete Tick Aggregation event to its registered event-family extension handler.
    /// </summary>
    /// <param name="context">The event actor context used by the handler.</param>
    /// <param name="event">The event to dispatch.</param>
    /// <returns>A task representing the asynchronous handler execution.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="context"/> or <paramref name="event"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the event's concrete type has no registered receive handler.
    /// </exception>
    protected override async ValueTask ReceiveAsync(IEventActorContext context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);

        if (!_receiveMap.TryGetValue(@event.GetType().Name, out var receiveHandler))
            throw new InvalidOperationException(
                $"Unable to resolve {ActorName} event from message: {@event.Subject}");

        _ = await receiveHandler.Invoke(@event, context).ConfigureAwait(false);
    }

    /// <summary>
    /// Publishes the framework event-error notification for an exception raised during event processing.
    /// </summary>
    /// <param name="context">The event actor context used to publish the error notification.</param>
    /// <param name="threadId">The actor thread on which processing failed.</param>
    /// <param name="event">The event being processed when the exception occurred.</param>
    /// <param name="exception">The processing exception.</param>
    /// <returns>A task representing asynchronous error notification.</returns>
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
