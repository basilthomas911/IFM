using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Application.Actor.Event;

/// <summary>
/// Represents an event actor responsible for processing fund transaction-related events within the actor system. Provides
/// mechanisms for parsing incoming messages, handling event execution, managing actor state, and reporting errors
/// specific to fund events.
/// </summary>
/// <param name="supervisor">The actor supervisor that manages actor lifecycle and coordinates event processing within the system. Cannot be
/// null.</param>
/// <param name="logger">The logger used to record diagnostic and operational information for the fund event actor. Cannot be null.</param>
public class ApplicationEventActor(IActorSupervisor supervisor, ILogger<ApplicationEventActor> logger)
    : BaseEventActor<ApplicationEventActor>(supervisor, logger, new ActorMailboxId(ActorType.Event, Actor))
{
    public const string Actor = "FundTransactionEvent";
    readonly Dictionary<string, Func<IEvent, IEventActorContext, ILogger, ValueTask<bool>>> _receiveMap = [];

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a corresponding event based on the message
    /// subject and verb.
    /// </summary>
    /// <param name="context">The actor context used for event processing. Cannot be null.</param>
    /// <param name="message">The NATS message containing the event data to parse. Cannot be null.</param>
    /// <returns>An event object representing the parsed event corresponding to the message and verb, or <see langword="null"/> if the message subject
    /// does not correspond to a known event (indicating the message should be ignored).</returns>
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
    /// <remarks>This dictionary enables efficient deserialization of incoming NATS messages by associating
    /// each event verb with a function that constructs the appropriate event type. The mapping assumes that each verb
    /// is unique and corresponds to a specific event class. The functions expect the message payload to be compatible
    /// with the target event type.</remarks>
    static readonly Dictionary<string, Func<NatsMsg<byte[]>, IEvent>> _parseMap = [];

    /// <summary>
    /// Handles the execution of a received event by invoking the corresponding processing function based on the event's
    /// </summary>
    /// <param name="context"></param>
    /// <param name="state"></param>
    /// <param name="event"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected override async ValueTask ReceiveAsync(IEventActorContext context, IActorState state, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        var eventName = @event.GetType().Name;
        _ = _receiveMap.ContainsKey(eventName)
            ? await _receiveMap[eventName](@event, context, logger)
            : throw new InvalidOperationException($"Unable to resolve {Actor} event from message: {@event.Subject}");
    }

    /// <summary>
    /// Handles an exception that occurs during event actor processing and returns a failed service result containing
    /// error details.
    /// </summary>
    /// <remarks>This method sends an error event to the actor system to record the exception and returns a
    /// standardized failure result. The returned result can be used to propagate error details to callers or for
    /// logging purposes.</remarks>
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
