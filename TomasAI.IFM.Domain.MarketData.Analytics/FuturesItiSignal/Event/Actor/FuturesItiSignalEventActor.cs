using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Actor;

/// <summary>
/// Represents an event actor responsible for processing futures ITI signal events within the actor system. Provides
/// mechanisms for parsing incoming messages, handling event execution, managing actor state, and reporting errors
/// specific to futures ITI signal events.
/// </summary>
/// <param name="supervisor">The actor supervisor that manages actor lifecycle and coordinates event processing within the system. Cannot be
/// null.</param>
/// <param name="logger">The logger used to record diagnostic and operational information for the futures ITI signal event actor. Cannot be null.</param>
public class FuturesItiSignalEventActor(
    IActorSupervisor supervisor, 
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesItiSignalEventActor> logger)
    : BaseEventActor<FuturesItiSignalEventActor>(supervisor, logger, new ActorMailboxId(ActorType.Event, Actor))
{
    public const string Actor = "FuturesItiSignalEvent";
    readonly Dictionary<string, Func<IEvent, IEventActorContext, IStatusConsoleWriter, ILogger, ValueTask<bool>>> _receiveMap = new()
    {
        [typeof(FuturesEodDataInsertedCompleteEvent).Name] = async (evt, context, statusConsoleWriter, logger) =>
        {
            var e = (evt as FuturesEodDataInsertedCompleteEvent)!;
            return await e.ExecuteAsync(context, statusConsoleWriter, logger);
        },
        [typeof(FuturesItiSignalGeneratedCompleteEvent).Name] = async (evt, context, statusConsoleWriter, logger) =>
        {
            var e = (evt as FuturesItiSignalGeneratedCompleteEvent)!;
            return await e.ExecuteAsync(context, statusConsoleWriter, logger);
        }
    };

    /// <summary>
    /// Initializes the actor's startup process and configures event routing for the specified context.
    /// </summary>
    /// <remarks>This method sets up the event routing for the actor, specifically for receiving FuturesEodDataInsertedCompleteEvent.</remarks>
    /// <param name="context">The context in which the event actor operates. Used to add event routers for handling events.</param>
    /// <returns>A task that represents the asynchronous operation of the startup process.</returns>
    protected override async ValueTask OnStartup(IEventActorContext context)
    {
        context.AddEventRouter(new ActorTypeId(ActorType.Event, FuturesEodDataInsertedCompleteEvent.Actor, FuturesEodDataInsertedCompleteEvent.Verb), Id);
        await ValueTask.CompletedTask;
    }

    /// <summary>
    /// Handles the shutdown process for the event actor, ensuring that event routing is properly cleaned up.
    /// </summary>
    /// <remarks>This method is called when the actor is shutting down. It removes the associated event router
    /// to prevent further event handling and to release resources.</remarks>
    /// <param name="context">The context in which the event actor operates. Used to manage event routing and actor lifecycle operations.</param>
    /// <returns>A completed ValueTask that indicates the shutdown operation has been processed.</returns>
    protected override async ValueTask OnShutdown(IEventActorContext context)
    {
        context.RemoveEventRouter(new ActorTypeId(ActorType.Event, FuturesEodDataInsertedCompleteEvent.Actor, FuturesEodDataInsertedCompleteEvent.Verb), Id);
        await ValueTask.CompletedTask;
    }

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
    static readonly Dictionary<string, Func<NatsMsg<byte[]>, IEvent>> _parseMap = new()
    {
        [FuturesEodDataInsertedCompleteEvent.Verb] = msg => msg.AsEvent<FuturesEodDataInsertedCompleteEvent>()!,
        [FuturesItiSignalGeneratedCompleteEvent.Verb] = msg => msg.AsEvent<FuturesItiSignalGeneratedCompleteEvent>()!
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
        _ = await receiveFunc.Invoke(@event, context, statusConsoleWriter, logger);
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
