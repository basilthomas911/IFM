using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Application.Actor.Command.Handlers;
using TomasAI.IFM.Domain.Application.Actor.Command.State;
using TomasAI.IFM.Domain.Application.Shared.Commands;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Application.EventProjector.Contracts;

namespace TomasAI.IFM.Domain.Application.Actor.Command.Actor;

/// <summary>
/// Represents an actor responsible for managing application commands and state within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="ApplicationCommandActor"/> is a specialised command actor designed to handle operations
/// related to the application lifecycle. It processes commands such as starting and shutting down the application,
/// validates the commands, and manages the actor's state. This actor relies on an event-sourced repository for state
/// persistence and uses dependency injection to resolve required services.</remarks>
/// <param name="dbEventSource">The event source database context used for logging and persisting command events.</param>
/// <param name="logger">The logger used to record diagnostic and operational information for the actor.</param>
public sealed class ApplicationCommandActor(
    IEventSourceActorDbContext dbEventSource,
    IEventProjector<ApplicationCommandActor> eventProjector,
    ILogger<ApplicationCommandActor> logger)
    : BaseEventSourceCommandActor<ApplicationCommandActor>(logger, new ActorMailboxId(ActorType.Command, ActorName))
{
    public const string ActorName = "ApplicationCommand";
    readonly IEventSourceActorDbContext _dbEventSource = IsArgumentNull.Set(dbEventSource);
    readonly IEventProjector<ApplicationCommandActor> _eventProjector = IsArgumentNull.Set(eventProjector);
    IEventSourceActorStateRepository<ApplicationCommandState> _repo = default!;

    /// <summary>
    /// Performs initialisation logic when the actor starts up.
    /// </summary>
    /// <param name="context">The <see cref="ICommandActorContext"/> providing access to the actor's dependencies and runtime context.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    protected override async ValueTask OnStartup(ICommandActorContext context)
    {
        IsArgumentNull.Check(context);
        _repo = IsArgumentNull.Set(context.Container.Resolve<IEventSourceActorStateRepository<ApplicationCommandState>>());
        await _eventProjector.StartAsync(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a command instance for the specified actor context.
    /// </summary>
    /// <param name="context">The actor context used to resolve and process the command. Cannot be null.</param>
    /// <param name="message">The NATS message containing the command data to be parsed.</param>
    /// <returns>An <see cref="ICommand"/> instance representing the parsed command from the message.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject does not correspond to a known command for the actor.</exception>
    protected override ICommand ParseMessage(ICommandActorContext context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject;
        if (msgSubject is not { ActorType: ActorType.Command, Name: ActorName })
            throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {message.Subject}");

        ICommand? command = msgSubject.Verb switch
        {
            StartApplicationCommand.Verb => message.AsCommand<StartApplicationCommand>(),
            ShutdownApplicationCommand.Verb => message.AsCommand<ShutdownApplicationCommand>(),
            _ => throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {message.Subject}")
        };
        IsArgumentNull.Check(command);
        return command;
    }

    /// <summary>
    /// Processes the specified command asynchronously within the given actor context and state, and returns a result
    /// containing the command's unique identifier.
    /// </summary>
    /// <param name="context">The actor context in which the command is received. Cannot be null.</param>
    /// <param name="state">The current state of the actor. Must be a valid instance of <see cref="ApplicationCommandState"/>. Cannot be null.</param>
    /// <param name="cmd">The command to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation. The result contains a ServiceResult wrapping a
    /// GuidResult with the command's unique identifier.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the command type cannot be resolved from the message.</exception>
    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext context, IActorState state, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(cmd);
        var applicationState = IsArgumentNull.Set((state as ApplicationCommandState)!);

        // ParseMessage is synchronous because the actor contract must release its pooled
        // payload immediately after materialization. Persist the log here so storage I/O
        // remains asynchronous and never blocks the mailbox worker thread.
        await _dbEventSource
            .InsertCommandLogAsync(cmd, DateTime.UtcNow, JsonConvert.SerializeObject(cmd))
            .ConfigureAwait(false);

        _ = cmd switch
        {
            StartApplicationCommand start => start.Execute(applicationState),
            ShutdownApplicationCommand shutdown => shutdown.Execute(applicationState),
            _ => throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {cmd.Subject}")
        };

        return new ServiceOk<GuidResult>(new GuidResult(cmd.CommandId));
    }

    /// <summary>
    /// Validates the current command asynchronously within the specified command actor context.
    /// </summary>
    /// <param name="context">The context in which the command is being executed, providing access to services and dependencies.</param>
    /// <param name="threadId">The identifier of the actor thread for which validation is being performed.</param>
    /// <param name="cmd">The command to be validated. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous validation operation.</returns>
    protected override ValueTask OnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(cmd);

        if (cmd is not StartApplicationCommand and not ShutdownApplicationCommand)
            throw new InvalidOperationException($"Unable to validate {ActorName} commands from message: {cmd.Subject}");

        // These lifecycle commands have one invariant. Avoid allocating a List,
        // ValidationError and StringBuilder for every valid message.
        if (cmd.CommandId == Guid.Empty)
            throw new CommandValidationException(cmd.ErrorCode, $"{cmd.CommandName}.CommandId is empty{Environment.NewLine}");

        return ValueTask.CompletedTask;
    }

    protected override async ValueTask OnShutdown(ICommandActorContext context)
        => await _eventProjector.StopAsync().ConfigureAwait(false);

    /// <summary>
    /// Asynchronously loads the state for the actor using the specified command context and thread identifier.
    /// </summary>
    /// <param name="context">The context of the command actor, providing information about the current command execution.</param>
    /// <param name="threadId">The identifier of the actor thread on which the state is being loaded.</param>
    /// <param name="cmd">The command for which state is being loaded. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation. The task result contains the
    /// loaded actor state.</returns>
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(cmd);
        return await _repo.LoadStateAsync(cmd).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously saves the current state of the application actor in response to a command.
    /// </summary>
    /// <param name="context">The context for the actor command execution, providing access to actor metadata and runtime services. Cannot be null.</param>
    /// <param name="threadId">The identifier of the actor thread on which the command is being executed. Cannot be null.</param>
    /// <param name="state">The current state of the actor to be persisted. Must be a non-null instance of <see cref="ApplicationCommandState"/>.</param>
    /// <param name="cmd">The command that triggered the state save operation. Cannot be null.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous save operation.</returns>
    protected override async ValueTask OnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand cmd)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(cmd);
        var applicationState = IsArgumentNull.Set((state as ApplicationCommandState)!);
        await _repo.SaveStateAsync(context, applicationState, cmd).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles exceptions that occur during command execution and returns a failed service result containing error
    /// event information.
    /// </summary>
    /// <param name="context">The command actor context in which the exception occurred.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception was encountered.</param>
    /// <param name="command">The command that encountered the exception.</param>
    /// <param name="ex">The exception that was thrown during command processing.</param>
    /// <returns>A failed service result containing a GUID result and error event details describing the failure.</returns>
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd, CancellationToken cancellationToken)
        => await _repo.LoadStateAsync(cmd, cancellationToken).ConfigureAwait(false);

    protected override async ValueTask OnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand cmd, CancellationToken cancellationToken)
        => await _repo.SaveStateAsync(context, (ApplicationCommandState)state, cmd, cancellationToken).ConfigureAwait(false);

    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception ex)
    {
        try
        {
            IsArgumentNull.Check(context);
            var cmdErrorEvent = await ex
                .SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent, ActorEntityId>(ErrorType.Command, context)
                .ConfigureAwait(false);
            return new ServiceFailed<GuidResult>(cmdErrorEvent);
        }
        catch (Exception innerEx)
        {
            logger.LogError(innerEx, "Error handling exception for {Actor} command in thread {ThreadId}: {OriginalExceptionMessage}", ActorName, threadId, ex.Message);
            return CommandFailed(innerEx, command);
        }
    }
}
