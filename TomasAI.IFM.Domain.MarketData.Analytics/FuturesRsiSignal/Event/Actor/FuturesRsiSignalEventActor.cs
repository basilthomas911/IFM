using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;

public class FuturesRsiSignalEventActor(
    IActorSupervisor supervisor, 
    IActorMarketDataAnalyticsCommandApiFactory commandApiFactory,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesRsiSignalEventActor> logger,
    IBlackboardService blackboardService)
    : BaseEventActor<FuturesRsiSignalEventActor>(supervisor, logger, new ActorMailboxId(ActorType.Event, Actor))
{
    public const string Actor = "FuturesRsiSignalEvent";
    IActorMarketDataAnalyticsCommandApi? _commandApi;
    readonly Dictionary<string, Func<IEvent, IEventActorContext, IActorMarketDataAnalyticsCommandApi, ValueTask<bool>>> _receiveMap = new()
    {
        [typeof(FuturesRsiSignalStartedEvent).Name] = async (evt, context, commandApi) =>
        {
            var e = (evt as FuturesRsiSignalStartedEvent)!;
            return await e.ExecuteAsync(context, commandApi, statusConsoleWriter, logger);
        },
        [typeof(FuturesRsiSignalStoppedEvent).Name] = async (evt, context, _) =>
        {
            var e = (evt as FuturesRsiSignalStoppedEvent)!;
            return await e.ExecuteAsync(context, statusConsoleWriter, logger);
        },
        [typeof(FuturesRsiSignalGeneratedEvent).Name] = async (evt, context, _) =>
        {
            var e = (evt as FuturesRsiSignalGeneratedEvent)!;
            return await e.ExecuteAsync(context, statusConsoleWriter, logger, blackboardService);
        }
    };

    protected override ValueTask OnStartup(IEventActorContext context)
    {
        _ = GetCommandApi(context);
        return ValueTask.CompletedTask;
    }

    IActorMarketDataAnalyticsCommandApi GetCommandApi(IEventActorContext context)
        => _commandApi ??= commandApiFactory.Create(context);

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
        [FuturesRsiSignalStartedEvent.Verb] = msg => msg.AsEvent<FuturesRsiSignalStartedEvent>()!,
        [FuturesRsiSignalStoppedEvent.Verb] = msg => msg.AsEvent<FuturesRsiSignalStoppedEvent>()!,
        [FuturesRsiSignalGeneratedEvent.Verb] = msg => msg.AsEvent<FuturesRsiSignalGeneratedEvent>()!
    };

    /// <summary>
    /// Asynchronously processes an event received by the event actor using the appropriate event handler.
    /// </summary>
    /// <param name="context">The context in which the event actor is executing. Cannot be null.</param>
    /// <param name="event">The event to be processed by the event actor. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous receive operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no handler is registered for the event type.</exception>
    protected override async ValueTask ReceiveAsync(IEventActorContext context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        var eventName = @event.GetType().Name;
        if (!_receiveMap.TryGetValue(eventName, out var receiveFunc))
            throw new InvalidOperationException($"Unable to resolve {Actor} event from message: {@event.Subject}");
        _ = await receiveFunc.Invoke(@event, context, GetCommandApi(context));
    }

    /// <summary>
    /// Handles an exception that occurs during event actor processing and returns a failed service result containing
    /// error details.
    /// </summary>
    /// <param name="context">The event actor context in which the exception occurred.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception was raised.</param>
    /// <param name="event">The event being processed when the exception was thrown.</param>
    /// <param name="ex">The exception that was thrown during actor processing.</param>
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
