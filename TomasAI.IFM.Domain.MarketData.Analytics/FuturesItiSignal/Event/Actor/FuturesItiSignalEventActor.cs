using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;

using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;

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
    IEventActorContext<FuturesItiSignalEventActor> actorContext)
    : BaseEventActor<FuturesItiSignalEventActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IFuturesItiSignalEventContext ActorContext =>
        IsArgumentNull.Set(Context as IFuturesItiSignalEventContext, nameof(Context))!;

    public const string Actor = "FuturesItiSignalEvent";
    readonly IReadOnlyDictionary<Type, Func<IEvent, IEventActorContext<FuturesItiSignalEventActor>, IStatusConsoleWriter, ILogger, ValueTask<bool>>> _receiveMap = new Dictionary<Type, Func<IEvent, IEventActorContext<FuturesItiSignalEventActor>, IStatusConsoleWriter, ILogger, ValueTask<bool>>>()
    {
        [typeof(FuturesItiSignalGeneratedCompleteEvent)] = async (evt, context, statusConsoleWriter, logger) =>
        {
            var e = (evt as FuturesItiSignalGeneratedCompleteEvent)!;
            return await e.ExecuteAsync(context, statusConsoleWriter, logger);
        }
    };

    /// <summary>
    /// Initializes the actor's startup process and configures event routing for the specified context.
    /// </summary>
    /// <remarks>The generated-event family is addressed directly to this actor.</remarks>
    /// <param name="context">The context in which the event actor operates. Used to add event routers for handling events.</param>
    /// <returns>A task that represents the asynchronous operation of the startup process.</returns>
    protected override async ValueTask OnStartup(IEventActorContext<FuturesItiSignalEventActor> context)
    {
        _ = context;
        await ValueTask.CompletedTask;
    }

    /// <summary>
    /// Handles the shutdown process for the event actor, ensuring that event routing is properly cleaned up.
    /// </summary>
    /// <remarks>This method is called when the actor is shutting down. It removes the associated event router
    /// to prevent further event handling and to release resources.</remarks>
    /// <param name="context">The context in which the event actor operates. Used to manage event routing and actor lifecycle operations.</param>
    /// <returns>A completed ValueTask that indicates the shutdown operation has been processed.</returns>
    protected override async ValueTask OnShutdown(IEventActorContext<FuturesItiSignalEventActor> context)
    {
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
    protected override IEvent ParseMessage(IEventActorContext<FuturesItiSignalEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    /// <summary>
    /// Maps event verb strings to factory functions that convert NATS messages into corresponding event instances.
    /// </summary>
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>()
    {
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
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesItiSignalEventActor> context, IEvent @event)
    {
        var dispatchContext = context;
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        var receiveFunc = ResolveMappedEventHandler(@event, _receiveMap);
        _ = await receiveFunc.Invoke(@event, dispatchContext, ActorContext.StatusConsoleWriter, ActorContext.Logger);
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
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FuturesItiSignalEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
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
            Context.Logger.LogError(innerEx, "Failed to send EventExceptionEvent for {Actor} actor.", Actor);
        }
    }
}
