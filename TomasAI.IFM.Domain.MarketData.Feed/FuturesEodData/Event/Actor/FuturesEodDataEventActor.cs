using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Blackboard;
using global::TomasAI.IFM.Shared.EventModelActor;
using global::TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Actor;

public class FuturesEodDataEventActor(
    IActorSupervisor supervisor, 
    IActorMarketDataFeedEventApiFactory eventApiFactory,
    IBlackboardService blackboardService,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesEodDataEventActor> logger)
    : BaseEventActor<FuturesEodDataEventActor>(supervisor, logger, new ActorMailboxId(ActorType.Event, Actor))
{
    public const string Actor = "FuturesEodDataEvent";
    IActorMarketDataFeedEventApi? _eventApi;
    readonly FuturesEodDataEventParameters _eventParameters = new(
        blackboardService, statusConsoleWriter, logger);
    readonly Dictionary<string, Func<IEvent, IEventActorContext, IActorMarketDataFeedEventApi, FuturesEodDataEventParameters, ValueTask<bool>>> _receiveMap = new()
    {
        [typeof(FuturesEodDataInsertedEvent).Name] = async (evt, context, eventApi, eventParams) =>
        {
            var e = (evt as FuturesEodDataInsertedEvent)!;
            return await e.ExecuteAsync(context, eventApi, eventParams);
        },
        [typeof(FuturesEodDataInsertedCompleteEvent).Name] = async (evt, _, eventApi, eventParams) =>
        {
            var e = (evt as FuturesEodDataInsertedCompleteEvent)!;
            return await e.ExecuteAsync(eventApi, eventParams);
        },
        [typeof(VixFuturesEodDataInsertedCompleteEvent).Name] = async (evt, context, _, eventParams) =>
        {
            var e = (evt as VixFuturesEodDataInsertedCompleteEvent)!;
            return await e.ExecuteAsync(context, eventParams);
        }
    };

    protected override ValueTask OnStartup(IEventActorContext context)
    {
        _ = GetEventApi(context);
        return ValueTask.CompletedTask;
    }

    IActorMarketDataFeedEventApi GetEventApi(IEventActorContext context)
        => _eventApi ??= eventApiFactory.Create(context);

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a corresponding event based on the message
    /// subject and verb.
    /// </summary>
    /// <param name="context">The actor context used for event processing. Cannot be null.</param>
    /// <param name="message">The NATS message containing the event data to parse. Cannot be null.</param>
    /// <returns>An event object representing the parsed event corresponding to the message and verb.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject does not correspond to a known event or if the event cannot be
    /// resolved from the message.</exception>
    protected override IEvent ParseMessage(IEventActorContext context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject;
        if (msgSubject is not { ActorType: ActorType.Event, Name: Actor }
            || !_parseMap.TryGetValue(msgSubject.Verb, out var messageParser))
            return default!;
        var @event = messageParser.Invoke(message);
        IsArgumentNull.Check(@event);
        @event.CheckForEmptyCommandId();
        return @event;
    }

    /// <summary>
    /// Maps event verb strings to factory functions that convert NATS messages into corresponding event instances.
    /// </summary>
    static readonly Dictionary<string, Func<IActorMessage, IEvent>> _parseMap = new()
    {
        [FuturesEodDataInsertedEvent.Verb] = msg => msg.AsEvent<FuturesEodDataInsertedEvent>()!,
        [FuturesEodDataInsertedCompleteEvent.Verb] = msg => msg.AsEvent<FuturesEodDataInsertedCompleteEvent>()!,
        [VixFuturesEodDataInsertedCompleteEvent.Verb] = msg => msg.AsEvent<VixFuturesEodDataInsertedCompleteEvent>()!
    };

    /// <summary>
    /// Asynchronously processes an event received by the event actor using the appropriate event handler.
    /// </summary>
    /// <param name="context">The context in which the event actor is executing. Provides access to actor state and services required
    /// for event handling. Cannot be null.</param>
    /// <param name="event">The event to be processed by the event actor. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous receive operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no handler is registered for the event type, or if the event cannot be resolved from the message.</exception>
    protected override async ValueTask ReceiveAsync(IEventActorContext context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        var eventName = @event.GetType().Name;
        if (!_receiveMap.TryGetValue(eventName, out var receiveFunc))
            throw new InvalidOperationException($"Unable to resolve {Actor} event from message: {@event.Subject}");
        _ = await receiveFunc.Invoke(@event, context, GetEventApi(context), _eventParameters);
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
            await ex.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context);
        }
        catch (Exception innerEx)
        {
            await innerEx.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context);
            logger.LogError(innerEx, "Failed to send EventExceptionEvent for {Actor} actor.", Actor);
        }
    }
}
