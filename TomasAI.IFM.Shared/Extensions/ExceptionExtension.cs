using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Shared.Extensions;

/// <summary>
/// Provides extension methods for creating and sending standardized error events based on exceptions.
/// These helpers construct error event objects from exceptions and command metadata and send them via an
/// <see cref="ICommandActorContext"/>.
/// </summary>
public static class ExceptionExtension
{
    /// <summary>
    /// Static constructor to initialize any static state for the <see cref="ExceptionExtension"/> class.
    /// </summary>
    static ExceptionExtension()
    {
    }

    /// <summary>
    /// Returns the most specific error message by walking inner exceptions to the root cause.
    /// </summary>
    /// <param name="ex">The exception to inspect. Must not be null.</param>
    /// <returns>The message of the innermost exception.</returns>
    public static string GetErrorMessage(this Exception ex)
    {
        while (ex.InnerException != null)
            ex = ex.InnerException;
        return ex.Message;
    }

    /// <summary>
    /// Creates a strongly-typed failed error event instance for a command using information from the provided
    /// exception and command instance.
    /// </summary>
    /// <typeparam name="TFailedEvent">The concrete error event type to create. Must implement <see cref="IErrorEvent{TEntityId}"/>.</typeparam>
    /// <typeparam name="TEntityId">The actor entity id type associated with the command/event.</typeparam>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <param name="command">The command associated with the error. Used to populate metadata on the error event.</param>
    /// <param name="actor">The actor name to use for the event subject.</param>
    /// <param name="verb">The verb (event name) to use for the event subject.</param>
    /// <returns>An instance of <typeparamref name="TFailedEvent"/> populated with metadata derived from the command and exception.</returns>
    public static TFailedEvent GetCommandFailedEvent<TFailedEvent, TEntityId>(this Exception ex, ICommand<TEntityId> command, string actor, string verb)
        where TEntityId : IActorEntityId
        where TFailedEvent : IErrorEvent<TEntityId>
    {
        var e = Activator.CreateInstance<TFailedEvent>();
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IEvent.Subject), new ActorSubject(ActorType.Event, actor, verb, command.EntityId.Format()));
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IEvent<TEntityId>.EntityId), command.EntityId);
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IEvent.CommandId), command.CommandId);
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.CommandName), command.GetType().Name);
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorType), ErrorType.Command);
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorMessage), ex.Message);
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorCode), command.ErrorCode);
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorData), $"{ex}");
        try { EventModelActor.EventInitHelper.SetProperty(e, nameof(IEvent.AggregateId), command.StreamId); } catch { }
        try { EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.CommandData), JsonConvert.SerializeObject(command, Formatting.Indented)); } catch { }
        return e;
    }

    /// <summary>
    /// Constructs a failed error event from the provided exception and command, then sends it asynchronously to the
    /// supplied <see cref="ICommandActorContext"/>.
    /// </summary>
    /// <typeparam name="TFailedEvent">The concrete error event type to create and send.</typeparam>
    /// <typeparam name="TEntityId">The actor entity id type associated with the error event.</typeparam>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <param name="context">The command actor context used to send the constructed error event.</param>
    /// <param name="command">The command associated with the failure.</param>
    /// <param name="actor">The actor name to use for the event subject.</param>
    /// <param name="verb">The verb (event name) to use for the event subject.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that completes with the error event instance that was sent.</returns>
    public static async ValueTask<TFailedEvent> SendErrorEventAsync<TFailedEvent, TEntityId>(this Exception ex, ICommandActorContext context, ICommand<TEntityId> command, string actor, string verb)
            where TFailedEvent : class, IErrorEvent<TEntityId>
            where TEntityId : IActorEntityId
    {
        var errorEvent = GetCommandFailedEvent<TFailedEvent, TEntityId>(ex, command, actor, verb);
        await context.SendAsync<TFailedEvent, TEntityId>(errorEvent);
        return errorEvent;
    }

    /// <summary>
    /// Creates a generic command exception error event populated with information about the provided exception
    /// and optional command/context metadata.
    /// </summary>
    /// <typeparam name="TFailedEvent">The concrete error event type to create.</typeparam>
    /// <typeparam name="TEntityId">The actor entity id type associated with the error event.</typeparam>
    /// <param name="ex">The exception that caused the error.</param>
    /// <param name="errorType">The classification of the error.</param>
    /// <param name="command">The command related to the error, if any.</param>
    /// <param name="entityId">The actor entity id to use for the event subject.</param>
    /// <param name="actor">The actor name to use for the event subject.</param>
    /// <param name="verb">The verb (event name) to use for the event subject.</param>
    /// <returns>An instance of <typeparamref name="TFailedEvent"/> populated with exception and command data.</returns>
    public static TFailedEvent GetCommandExceptionEvent<TFailedEvent, TEntityId>(this Exception ex, ErrorType errorType, ICommand command, IActorEntityId entityId, string actor, string verb)
    where TEntityId : IActorEntityId
    where TFailedEvent : IErrorEvent<TEntityId>
    {
        var e = Activator.CreateInstance<TFailedEvent>();
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IEvent.Subject), new ActorSubject(command is null ? ActorType.Supervisor : ActorType.Event, actor, verb, entityId.Format()));
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IEvent.CommandId), command?.CommandId ?? Guid.Empty);
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.CommandName), command?.GetType().Name ?? $"{errorType}");
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorType), errorType);

        // Handle AggregateException specially to include all inner exception messages
        if (ex is AggregateException aggEx)
        {
            var messages = aggEx.InnerExceptions.Select(innerEx => innerEx.Message);
            EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorMessage), string.Join("; ", messages));
        }
        else
        {
            EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorMessage), ex.Message);
        }

        EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorCode), command?.ErrorCode ?? 9999);
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorData), $"{ex}");
        try { EventModelActor.EventInitHelper.SetProperty(e, nameof(IEvent.AggregateId), command?.StreamId ?? string.Empty); } catch { }
        try { EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.CommandData), command is null ? string.Empty : JsonConvert.SerializeObject(command, Formatting.Indented)); } catch { }
        return e;
    }

    public static TFailedEvent GetEventExceptionEvent<TFailedEvent, TEntityId>(this Exception ex, ErrorType errorType, ICommand command, IActorEntityId entityId, string actor, string verb)
where TEntityId : IActorEntityId
where TFailedEvent : IErrorEvent<TEntityId>
    {
        var e = Activator.CreateInstance<TFailedEvent>();
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IEvent.Subject), new ActorSubject(command is null ? ActorType.Supervisor : ActorType.Event, actor, verb, entityId.Format()));
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IEvent.CommandId), command?.CommandId ?? Guid.Empty);
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.CommandName), command?.GetType().Name ?? $"{errorType}");
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorType), errorType);

        // Handle AggregateException specially to include all inner exception messages
        if (ex is AggregateException aggEx)
        {
            var messages = aggEx.InnerExceptions.Select(innerEx => innerEx.Message);
            EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorMessage), string.Join("; ", messages));
        }
        else
        {
            EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorMessage), ex.Message);
        }

        EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorCode), command?.ErrorCode ?? 9999);
        EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.ErrorData), $"{ex}");
        try { EventModelActor.EventInitHelper.SetProperty(e, nameof(IEvent.AggregateId), command?.StreamId ?? string.Empty); } catch { }
        try { EventModelActor.EventInitHelper.SetProperty(e, nameof(IErrorEvent.CommandData), command is null ? string.Empty : JsonConvert.SerializeObject(command, Formatting.Indented)); } catch { }
        return e;
    }


    /// <summary>
    /// Constructs a command exception error event using the provided exception and sends it asynchronously via the
    /// provided <see cref="ICommandActorContext"/>. Returns the error event instance that was sent.
    /// </summary>
    /// <typeparam name="TFailedEvent">The concrete error event type to create and send.</typeparam>
    /// <typeparam name="TEntityId">The actor entity id type associated with the error event.</typeparam>
    /// <param name="ex">The exception that triggered the error event.</param>
    /// <param name="errorType">The classification of the error.</param>
    /// <param name="context">The command actor context used to send the constructed error event.</param>
    /// <param name="command">The command related to the error, if any.</param>
    /// <param name="entityId">The actor entity id to use for the event subject.</param>
    /// <param name="actor">The actor name to use for the event subject.</param>
    /// <param name="verb">The verb (event name) to use for the event subject.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that completes with the error event instance that was sent.</returns>
    public static async ValueTask<TFailedEvent> SendErrorEventAsync<TFailedEvent, TEntityId>(this Exception ex, ErrorType errorType, ICommandActorContext context, ICommand command, IActorEntityId entityId, string actor, string verb)
       where TFailedEvent : class, IErrorEvent<TEntityId>
       where TEntityId : IActorEntityId
    {
        var errorEvent = GetCommandExceptionEvent<TFailedEvent, TEntityId>(ex, errorType, command, entityId, actor, verb);
        await context.SendAsync<TFailedEvent, TEntityId>(errorEvent);
        return errorEvent;
    }

    /// <summary>
    /// Sends an error event of the specified type to the provided command actor context, based on the given exception
    /// and error classification. If constructing the specialized error event fails, a generic command exception event
    /// will be created and sent instead. The method returns the error event instance that was sent.
    /// </summary>
    /// <typeparam name="TFailedEvent">The concrete error event type to create and send.</typeparam>
    /// <typeparam name="TEntityId">The actor entity id type associated with the error event.</typeparam>
    /// <param name="ex">The exception that triggered the error event.</param>
    /// <param name="errorType">The classification of the error to associate with the event.</param>
    /// <param name="context">The command actor context to which the error event will be sent.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains the error
    /// event that was sent.</returns>
    public static async ValueTask<TFailedEvent> SendErrorEventAsync<TFailedEvent, TEntityId>(this Exception ex, ErrorType errorType, ICommandActorContext context)
       where TFailedEvent : class, IErrorEvent<TEntityId>
       where TEntityId : IActorEntityId
    {
        var actorName = "CommandException";
        TFailedEvent errorEvent;
        try
        {
            errorEvent = ex switch
            {
                CommandValidationException
                    => GetCommandExceptionEvent<TFailedEvent, TEntityId>(ex, ErrorType.CommandValidation, default!, ActorEntityId.Default, actorName, CommandValidationExceptionEvent.CommandValidationFail),
                StorageException
                    => GetCommandExceptionEvent<TFailedEvent, TEntityId>(ex, ErrorType.Storage, default!, ActorEntityId.Default, actorName, StorageExceptionEvent.StorageFail),
                _ => GetCommandExceptionEvent<TFailedEvent, TEntityId>(ex, ErrorType.Command, default!, ActorEntityId.Default, actorName, EventModelActor.Events.CommandExceptionEvent.CommandFail),
            };
        }
        catch (Exception sendEx)
        {
            errorEvent = GetCommandExceptionEvent<TFailedEvent, TEntityId>(sendEx, ErrorType.Command, default!, ActorEntityId.Default, actorName, EventModelActor.Events.CommandExceptionEvent.CommandFail);
        }
        await context.SendAsync<TFailedEvent, TEntityId>(errorEvent);
        return errorEvent;
    }

    /// <summary>
    /// Sends an error event to the specified event actor context based on the provided exception and error type, and
    /// returns the dispatched error event instance.
    /// </summary>
    /// <remarks>If the exception type is recognized, a corresponding error event is created; otherwise, a
    /// generic command error event is sent. If an error occurs while creating the error event, a fallback error event
    /// is dispatched. The method is asynchronous and returns after the event has been sent.</remarks>
    /// <typeparam name="TFailedEvent">The type of error event to be sent. Must implement <see cref="IErrorEvent{TEntityId}"/>.</typeparam>
    /// <typeparam name="TEntityId">The type of the entity identifier associated with the error event. Must implement <see cref="IActorEntityId"/>.</typeparam>
    /// <param name="ex">The exception that triggered the error event. Cannot be null.</param>
    /// <param name="errorType">The classification of the error to be reported in the event.</param>
    /// <param name="context">The event actor context to which the error event will be sent. Cannot be null.</param>
    /// <returns>A <typeparamref name="TFailedEvent"/> instance representing the dispatched error event.</returns>
   public static async ValueTask<TFailedEvent> SendErrorEventAsync<TFailedEvent, TEntityId>(this Exception ex, ErrorType errorType, IEventActorContext context)
       where TFailedEvent : class, IErrorEvent<TEntityId>
       where TEntityId : IActorEntityId
    {
        var actorName = "EventException";
        TFailedEvent errorEvent;
        try
        {
            // For event actors, we always use ErrorType.EventService regardless of the exception type
            errorEvent = GetEventExceptionEvent<TFailedEvent, TEntityId>(ex, ErrorType.EventService, default!, ActorEntityId.Default, actorName, EventExceptionEvent.Verb);
            await context.SendAsync<TFailedEvent, TEntityId>(errorEvent);
        }
        catch (Exception sendEx)
        {
            errorEvent = GetEventExceptionEvent<TFailedEvent, TEntityId>(sendEx, ErrorType.EventService, default!, ActorEntityId.Default, actorName, EventExceptionEvent.Verb);
            try { await context.SendAsync<TFailedEvent, TEntityId>(errorEvent); } catch { }
        }
        return errorEvent;
    }
}


