using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

using TomasAI.IFM.Domain.Application.Actor.Event.Extensions;

namespace TomasAI.IFM.Domain.Application.Actor.Event.Actor;

/// <summary>
/// Represents an event actor responsible for receiving application lifecycle events within the actor system.
/// </summary>
/// <param name="supervisor">The actor supervisor that manages actor lifecycle and coordinates event processing within the system. Cannot be
/// null.</param>
/// <param name="logger">The logger used to record diagnostic and operational information for the application event actor. Cannot be null.</param>
public sealed class ApplicationEventActor(
    IEventActorContext<ApplicationEventActor> actorContext)
    : BaseEventActor<ApplicationEventActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    private IApplicationEventContext ActorContext =>
        IsArgumentNull.Set(Context as IApplicationEventContext, nameof(Context))!;

    public const string Actor = ApplicationStartupEvent.Actor;

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a corresponding event based on the message
    /// subject and verb.
    /// </summary>
    /// <param name="context">The actor context used for event processing. Cannot be null.</param>
    /// <param name="message">The NATS message containing the event data to parse. Cannot be null.</param>
    /// <returns>An event object representing the parsed event corresponding to the message and verb, or <see langword="null"/> if the message subject
    /// does not correspond to a known event (indicating the message should be ignored).</returns>
    protected override IEvent ParseMessage(IEventActorContext<ApplicationEventActor> context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject;
        if (msgSubject is not { ActorType: ActorType.Event, Name: Actor })
            return default!;

        IEvent? @event = msgSubject.Verb switch
        {
            ApplicationStartupEvent.Verb => message.AsEvent<ApplicationStartupEvent>(),
            ApplicationShutdownEvent.Verb => message.AsEvent<ApplicationShutdownEvent>(),
            _ => null
        };

        if (@event is null)
            return default!;
        @event.CheckForEmptyCommandId();
        return @event;
    }

    /// <summary>
    /// Handles the execution of a received event by invoking the corresponding processing function based on the event's
    /// </summary>
    /// <param name="context"></param>
    /// <param name="event"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected override ValueTask ReceiveAsync(IEventActorContext<ApplicationEventActor> context, IEvent @event)
    {
        var dispatchContext = context;
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);

        if (@event is not ApplicationStartupEvent and not ApplicationShutdownEvent)
            throw new InvalidOperationException($"Unable to resolve {Actor} event from message: {@event.Subject}");

        // Lifecycle events are intentionally broadcast notifications. External event
        // listeners perform the work; this domain actor only validates/acknowledges them.
        return ValueTask.CompletedTask;
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
    protected override async ValueTask OnExceptionAsync(IEventActorContext<ApplicationEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        try
        {
            IsArgumentNull.Check(context);
            await ex
                .SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context)
                .ConfigureAwait(false);
        }
        catch (Exception innerEx)
        {
            Context.Logger.LogError(innerEx, "Failed to send EventExceptionEvent for {Actor} actor.", Actor);
        }
    }
}
