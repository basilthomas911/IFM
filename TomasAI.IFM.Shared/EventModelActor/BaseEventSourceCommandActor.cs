using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides a base implementation for an actor that processes commands and manages its lifecycle, state, and messaging.
/// </summary>
/// <remarks>This abstract class serves as a foundation for implementing command-based actors in an actor system.
/// It provides lifecycle management, message handling, and state management capabilities. Derived classes must
/// implement specific behavior for message processing and state handling by overriding the appropriate protected
/// methods.</remarks>
/// <typeparam name="TActor">The type of the actor that this command actor represents.</typeparam>
/// <param name="logger"></param>
/// <param name="actorId"></param>
public abstract class BaseEventSourceCommandActor<TActor>(
     ILogger logger, ActorMailboxId actorId)
    : ICommandActor<TActor> where TActor : IActor
{
    readonly ActorMailboxId _actorId = IsArgumentNull.Set(actorId);
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    string _serviceId = string.Empty;

    ICommandActorContext? _context;
    IActorSupervisor _supervisor;

    // IActor properties
    public ActorMailboxId Id => _actorId;
    protected ILogger Logger => _logger;

    public IActorMailbox Mailbox { get; private set; } 
    public bool IsRunning { get; protected set; }
    public bool IsParent { get; protected set; }

    /// <summary>
    /// Asynchronously starts the actor and its associated components, including the mailbox, producer, and consumer.
    /// </summary>
    /// <remarks>This method initializes the actor's mailbox, starts the producer and consumer processes, and
    /// sets up the actor's command context. If the actor is already running, the method exits without performing any
    /// actions.</remarks>
    /// <param name="supervisor">The <see cref="IActorSupervisor"/> responsible for managing the actor's lifecycle and providing necessary
    /// resources.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the mailbox is not set before starting the actor.</exception>
    public async ValueTask StartAsync(IActorSupervisor supervisor)
    {
        IsArgumentNull.Check(supervisor);
        if (IsRunning) 
            return;

        // start up actor producers/consumers if set...
        _supervisor = supervisor;
        Mailbox = supervisor.CreateMailbox(_actorId);
        var producer = supervisor.GetProducer(_actorId);
        await producer.StartAsync(_actorId);
        _serviceId = typeof(TActor).Name;
        _logger.LogInformationEvent(_serviceId, "Started {MailboxId} producer.", _actorId);

        /// start any actor context processes...
        _context = supervisor.CreateCommandActorContext(actorId);
        await OnStartup(_context).ConfigureAwait(false);
        IsRunning = true;
    }

    /// <summary>
    /// Stops the actor and releases associated resources asynchronously.
    /// </summary>
    /// <remarks>This method ensures that the actor is properly shut down by invoking the shutdown logic and
    /// stopping any associated consumer or producer components, if they are present. If the actor is not running, the
    /// method returns immediately without performing any operations.</remarks>
    /// <param name="context">The context in which the actor is operating. This parameter provides access to actor-specific state and
    /// services.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous stop operation.</returns>
    public async ValueTask StopAsync()
    {
        if (!IsRunning) 
            return;

        // stop any actor producers/consumers if set...
        var producer = _supervisor.GetProducer(_actorId);
        await producer.StopAsync().ConfigureAwait(false);
        _logger.LogInformationEvent(_serviceId, "Stopped {MailboxId} producer.", _actorId);

        /// stop any  actor context processes...
        await _supervisor!.StopAsync(actorId).ConfigureAwait(false);
        await OnShutdown(_context!).ConfigureAwait(false);

        IsRunning = false;
    }

    /// <summary>
    /// Handles an incoming message for the actor, performing validation, state management, and message processing.
    /// </summary>
    /// <remarks>This method validates the message to ensure it is intended for the current actor, processes
    /// the message, and manages the actor's state by loading, updating, and saving it. If an exception occurs during
    /// processing, it is handled by invoking the exception handler.</remarks>
    /// <param name="message">The message to be processed, containing the subject and entity information.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message is not intended for the current actor or if the thread ID is invalid.</exception>
    public async ValueTask HandleMessageAsync(NatsMsg<byte[]> message)
    {
        var msgSubject = message.Subject.ToSubject();
        await HandleMessageAsync(message, msgSubject.ThreadId);
    }

    /// <summary>
    /// Handles an incoming message using a pre-resolved thread identifier, avoiding redundant subject parsing.
    /// </summary>
    /// <param name="message">The message to be processed.</param>
    /// <param name="threadId">The pre-resolved thread identifier from the caller.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask HandleMessageAsync(NatsMsg<byte[]> message, ActorThreadId threadId)
    {
        ICommand command = default!;
        int errorCode = 9998;
        ServiceResult<GuidResult> result;
        try
        {
            command = ParseMessage(_context!, message);

            /// get any existing error code from the message info...
            errorCode = command.ErrorCode;

            /// check if the message is a command and validate it...
            await OnValidateAsync(_context!, threadId, command);

            /// load the current state, process the message, and save the updated state...
            var state = await OnLoadStateAsync(_context!, threadId, command);
            state?.Id = threadId;

            /// process the message...
            result = await ReceiveAsync(_context!, state!, command);

            /// save the updated state...
            await OnSaveStateAsync(_context!, threadId, state!, command);

        }
        catch (Exception ex)
        {
            result = await OnExceptionAsync(_context!, threadId, command, ex);
        }

        /// reply with the result...
        await ActorExtensions.NatsReplyAsync(message, result);
    }

    // Explicit interface implementations forwarding to protected hooks
    ValueTask ICommandActor<TActor>.OnStartup(ICommandActorContext context) => OnStartup(context);
    ValueTask ICommandActor<TActor>.OnShutdown(ICommandActorContext context) => OnShutdown(context);
    ValueTask<ServiceResult<GuidResult>> ICommandActor<TActor>.ReceiveAsync(ICommandActorContext context, IActorState state, ICommand command) => ReceiveAsync(context, state, command);
    ValueTask ICommandActor<TActor>.OnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command) => OnValidateAsync(context, threadId, command);
    ValueTask<IActorState> ICommandActor<TActor>.OnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command) => OnLoadStateAsync(context, threadId, command);
    ValueTask ICommandActor<TActor>.OnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand command) => OnSaveStateAsync(context, threadId, state, command);
    ValueTask<ServiceResult<GuidResult>> ICommandActor<TActor>.OnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception ex) => OnExceptionAsync(context, threadId, command, ex);

    // Protected hooks for derived classes
    protected abstract ICommand ParseMessage(ICommandActorContext context, in NatsMsg<byte[]> message);
    protected virtual ValueTask OnStartup(ICommandActorContext context) => ValueTask.CompletedTask;
    protected virtual ValueTask OnShutdown(ICommandActorContext context) => ValueTask.CompletedTask;
    protected abstract ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext context, IActorState state, ICommand command);
    protected virtual ValueTask OnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command) => ValueTask.CompletedTask;
    protected virtual ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command )
    {
        return ValueTask.FromResult<IActorState>(default!);
    }

    protected virtual ValueTask OnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand command)
    {
        return ValueTask.CompletedTask;
    }

    protected abstract ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception ex);

    /// <summary>
    /// Creates a failed command event instance populated with error details and context information from the specified
    /// command and exception.
    /// </summary>
    /// <remarks>If <paramref name="command"/> is null, only the error details from <paramref name="ex"/> are
    /// included in the event. Otherwise, the event is populated with additional command context such as entity ID,
    /// command ID, and serialized command data. The returned event is created using the default constructor of
    /// <typeparamref name="TFailedEvent"/>.</remarks>
    /// <typeparam name="TFailedEvent">The type of error event to create. Must implement <see cref="IErrorEvent{TEntityId}"/> and have a parameterless
    /// constructor.</typeparam>
    /// <typeparam name="TEntityId">The type of the entity identifier associated with the command. Must implement <see cref="IActorEntityId"/>.</typeparam>
    /// <param name="command">The command that failed. Provides context for the error event. Can be null if command context is unavailable.</param>
    /// <param name="actor">The name or identifier of the actor responsible for the command.</param>
    /// <param name="verb">The action verb describing the command operation (for example, "Create", "Update").</param>
    /// <param name="ex">The exception that caused the command to fail. The exception's message and details are included in the error
    /// event.</param>
    /// <returns>An instance of <typeparamref name="TFailedEvent"/> containing error information and relevant command context.</returns>
    protected TFailedEvent GetCommandFailedEvent<TFailedEvent, TEntityId>(ICommand<TEntityId> command, string actor, string verb, Exception ex)
        where TEntityId : IActorEntityId
        where TFailedEvent : IErrorEvent<TEntityId>, new()
    {
        string aggregateId = string.Empty;
        try { aggregateId = command.StreamId; } catch { }
        string commandData = string.Empty;
        try { commandData = JsonConvert.SerializeObject(command, Formatting.Indented); } catch { }

        var e = new TFailedEvent
        {
            Subject = new ActorSubject(ActorType.Event, actor, verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            CommandId = command.CommandId,
            CommandName = command.GetType().Name,
            ErrorType = ErrorType.Command,
            ErrorMessage = ex.Message,
            ErrorCode = command.ErrorCode,
            ErrorData = $"{ex}",
            AggregateId = aggregateId,
            CommandData = commandData
        };
        return e;
    }

    /// <summary>
    /// Creates a failed command result containing details about the specified exception.
    /// </summary>
    /// <remarks>The returned result includes a new command identifier and default entity identifier. Use this
    /// method to standardize error reporting for failed commands.</remarks>
    /// <param name="ex">The exception that caused the command to fail. The exception's message is included in the error details.</param>
    /// <param name="cmd">Optional command that failed. If provided, its CommandId and ErrorCode will be included in the result.</param>
    /// <returns>A <see cref="ServiceFailed{GuidResult}"/> instance representing the failed command, including error information
    /// derived from the provided exception.</returns>
    protected ServiceFailed<GuidResult> CommandFailed(Exception ex, ICommand? cmd = default!)
        => new(new Events.CommandExceptionEvent
            {
                CommandId = cmd is null ? Guid.NewGuid() : cmd.CommandId,
                EntityId = ActorEntityId.Default,
                ErrorMessage = ex.Message,
                ErrorType = ErrorType.Command,
                ErrorCode = cmd?.ErrorCode ?? 0
            });
}
