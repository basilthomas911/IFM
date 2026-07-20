using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.Trade.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Command.State;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

public class MarketDataFeedEventActor(
    IActorSupervisor supervisor,
    IMarketDataApi marketDataApi,
    IOptionTradeLiveFeedMap optionTradeLiveFeedMap, 
    IBlackboardService blackboardService,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<MarketDataFeedEventActor> logger)
    : BaseEventActor<MarketDataFeedEventActor>(supervisor, logger, new ActorMailboxId(ActorType.Event, Actor))
{
    public const string Actor = "MarketDataFeedEvent";
    MarketDataFeedEventParameters _eventParameters = new(
        marketDataApi,
        optionTradeLiveFeedMap,
        blackboardService,
        statusConsoleWriter,
        logger);
    readonly Dictionary<string, Func<IEvent, IEventActorContext, MarketDataFeedEventParameters, ValueTask<bool>>> _receiveMap = new()
    {
        [typeof(MarketDataFeedStartedEvent).Name] = async (evt, ctx, eventParams) =>
        {
            var e = (evt as MarketDataFeedStartedEvent)!;
            return await e.ExecuteAsync(ctx, eventParams);
        },
        [typeof(MarketDataFeedStartedCompleteEvent).Name] = async (evt, ctx, eventParams) =>
        {
            var e = (evt as MarketDataFeedStartedCompleteEvent)!;
            return await e.ExecuteAsync(ctx, eventParams);
        },
        [typeof(MarketDataFeedStoppedEvent).Name] = async (evt, ctx, eventParams) =>
        {
            var e = (evt as MarketDataFeedStoppedEvent)!;
            return await e.ExecuteAsync(ctx, eventParams);
        },
        [typeof(MarketDataFeedStoppedCompleteEvent).Name] = async (evt, ctx, eventParams) =>
        {
            var e = (evt as MarketDataFeedStoppedCompleteEvent)!;
            return await e.ExecuteAsync(ctx, eventParams);
        },
        [typeof(MarketDataFeedResetEvent).Name] = async (evt, ctx, eventParams) =>
        {
            var e = (evt as MarketDataFeedResetEvent)!;
            return await e.ExecuteAsync(ctx, eventParams);
        },
        [typeof(MarketDataFeedResetCompleteEvent).Name] = async (evt, ctx, eventParams) =>
        {
            var e = (evt as MarketDataFeedResetCompleteEvent)!;
            return await e.ExecuteAsync(ctx, eventParams);
        }
    };

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a corresponding event based on the message
    /// subject and verb.
    /// </summary>
    /// <param name="context">The actor context used for event processing. Cannot be null.</param>
    /// <param name="message">The NATS message containing the event data to parse. Cannot be null.</param>
    /// <returns>An event object representing the parsed event corresponding to the message and verb.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject does not correspond to a known event or if the event cannot be
    /// resolved from the message.</exception>
    protected override IEvent ParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject.ToSubject();
        if (msgSubject is not { ActorType: ActorType.Event, Name: Actor }
            || !_parseMap.ContainsKey(msgSubject.Verb))
            return default!;
        var @event = _parseMap[msgSubject.Verb](message);
        IsArgumentNull.Check(@event);
        @event.CheckForEmptyCommandId();
        return @event;
    }

    /// <summary>
    /// Maps event verb strings to factory functions that convert NATS messages into corresponding event instances.
    /// </summary>
    static readonly Dictionary<string, Func<NatsMsg<byte[]>, IEvent>> _parseMap = new()
    {
        [MarketDataFeedStartedEvent.Verb] = msg => msg.AsEvent<MarketDataFeedStartedEvent>()!,
        [MarketDataFeedStartedCompleteEvent.Verb] = msg => msg.AsEvent<MarketDataFeedStartedCompleteEvent>()!,
        [MarketDataFeedStoppedEvent.Verb] = msg => msg.AsEvent<MarketDataFeedStoppedEvent>()!,
        [MarketDataFeedStoppedCompleteEvent.Verb] = msg => msg.AsEvent<MarketDataFeedStoppedCompleteEvent>()!,
        [MarketDataFeedResetEvent.Verb] = msg => msg.AsEvent<MarketDataFeedResetEvent>()!,
        [MarketDataFeedResetCompleteEvent.Verb] = msg => msg.AsEvent<MarketDataFeedResetCompleteEvent>()!
    };

    /// <summary>
    /// Asynchronously processes an event received by the event actor using the appropriate event handler.
    /// </summary>
    /// <param name="context">The context in which the event actor is executing. Provides access to actor state and services required
    /// for event handling. Cannot be null.</param>
    /// <param name="state">The current state of the actor, used to process the event. Cannot be null.</param>
    /// <param name="event">The event to be processed by the event actor. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous receive operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no handler is registered for the event type, or if the event cannot be resolved from the message.</exception>
    protected override async ValueTask ReceiveAsync(IEventActorContext context, IActorState state, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(@event);
        var eventName = @event.GetType().Name;
        _ = _receiveMap.ContainsKey(eventName)
            ? await _receiveMap[eventName](@event, context, _eventParameters)
            : throw new InvalidOperationException($"Unable to resolve {Actor} event from message: {@event.Subject}");
    }

    /// <summary>
    /// Handles an exception that occurs during event actor processing and returns a failed service result containing
    /// error details.
    /// </summary>
    /// <param name="context">The event actor context in which the exception occurred. Provides access to actor state and metadata relevant to
    /// error handling.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception was raised. Used to associate the error with the correct
    /// execution context.</param>
    /// <param name="event">The event being processed when the exception was thrown.</param>
    /// <param name="ex">The exception that was thrown during actor processing. Contains information about the error to be reported.</param>
    /// <returns>A task that represents the asynchronous exception handling operation.</returns>
    protected override async ValueTask OnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(@event);
            await ex.SendErrorEventAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context);
        }
        catch (Exception innerEx)
        {
            await innerEx.SendErrorEventAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context);
            logger.LogError(innerEx, "Failed to send EventExceptionEvent for {Actor} actor.", Actor);
        }
    }

}
