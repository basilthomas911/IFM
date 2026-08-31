using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides a base implementation for an actor that processes commands and manages its lifecycle, state, and messaging.
/// </summary>
/// <remarks>This abstract class serves as a foundation for implementing command-based actors in an actor system.
/// It provides lifecycle management, message handling, and state management capabilities. Derived classes must
/// implement specific behavior for message processing and state handling by overriding the appropriate protected
/// methods.</remarks>
/// <typeparam name="TActor">The type of the actor that this command actor represents.</typeparam>
/// <param name="actorContext">The closed-generic command context owned by the actor for its entire lifetime.</param>
/// <param name="logger">The logger used to record operational and diagnostic information.</param>
public abstract class BaseEventSourceCommandActor<TActor>(
    ICommandActorContext<TActor> actorContext,
    ILogger logger)
    : ICommandActor<TActor> where TActor : IActor
{
    readonly ICommandActorContext<TActor> _context = IsArgumentNull.Set(actorContext);
    readonly ActorMailboxId _actorId = IsArgumentNull.Set(actorContext).ActorId;
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    string _serviceId = string.Empty;

    ICommandAuditLogger? _commandAuditLogger;
    IActorSupervisor _supervisor;
    int _lifecycle;

    // IActor properties
    public ActorMailboxId Id => _actorId;
    /// <summary>
    /// Gets the closed-generic command context retained for the lifetime of this actor.
    /// </summary>
    protected ICommandActorContext<TActor> Context => _context;
    protected ILogger Logger => _logger;

    public IActorMailbox Mailbox { get; private set; } 
    public bool IsRunning
    {
        get => Volatile.Read(ref _lifecycle) == 2;
        protected set => Volatile.Write(ref _lifecycle, value ? 2 : 0);
    }
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
        => await StartAsync(supervisor, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask StartAsync(IActorSupervisor supervisor, CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(supervisor);
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _lifecycle, 1, 0) != 0)
            return;
        IActorProducer? producer = null;
        try
        {
            _supervisor = supervisor;
            Mailbox = supervisor.CreateMailbox(_actorId);
            producer = supervisor.GetProducer(_actorId);
            await producer.StartAsync(_actorId, cancellationToken).ConfigureAwait(false);
            _serviceId = typeof(TActor).Name;
            _logger.LogInformationEvent(_serviceId, "Started {MailboxId} producer.", _actorId);
            _commandAuditLogger = _context.Container.Resolve<ICommandAuditLogger>()
                ?? throw new InvalidOperationException(
                    $"{nameof(ICommandAuditLogger)} must be registered before command actors start.");
            cancellationToken.ThrowIfCancellationRequested();
            await OnStartup(_context, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _lifecycle, 2);
        }
        catch
        {
            if (producer is not null)
            {
                try { await producer.StopAsync().ConfigureAwait(false); }
                catch (Exception cleanupException) { _logger.LogError(cleanupException, "Failed to roll back {MailboxId} producer startup.", _actorId); }
            }
            Volatile.Write(ref _lifecycle, 0);
            throw;
        }
    }

    /// <summary>
    /// Stops the actor and releases associated resources asynchronously.
    /// </summary>
    /// <remarks>This method ensures that the actor is properly shut down by invoking the shutdown logic and
    /// stopping any associated consumer or producer components, if they are present. If the actor is not running, the
    /// method returns immediately without performing any operations. The running flag is cleared before asynchronous
    /// cleanup begins so concurrent or re-entrant stop requests are idempotent.</remarks>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous stop operation.</returns>
    public async ValueTask StopAsync()
        => await StopAsync(CancellationToken.None).ConfigureAwait(false);

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _lifecycle, 3, 2) != 2)
            return;
        try
        {
            // stop any actor producers/consumers if set...
            var producer = _supervisor.GetProducer(_actorId);
            // Once shutdown owns the actor lifecycle transition, finish cleanup atomically.
            await producer.StopAsync().ConfigureAwait(false);
            _logger.LogInformationEvent(_serviceId, "Stopped {MailboxId} producer.", _actorId);
        }
        finally
        {
            try
            {
                // Always release actor-owned lifecycle resources, including durable projector workers.
                await OnShutdown(_context!).ConfigureAwait(false);
            }
            finally
            {
                Volatile.Write(ref _lifecycle, 0);
            }
        }
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
    public ValueTask HandleMessageAsync(IActorMessage message)
        => HandleMessageAsync(message, message.Subject.ThreadId, CancellationToken.None);

    /// <summary>
    /// Handles an incoming message using a pre-resolved thread identifier, avoiding redundant subject parsing.
    /// </summary>
    /// <param name="message">The message to be processed.</param>
    /// <param name="threadId">The pre-resolved thread identifier from the caller.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId)
        => await HandleMessageAsync(message, threadId, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask HandleMessageAsync(
        IActorMessage message,
        ActorThreadId threadId,
        CancellationToken cancellationToken)
    {
        ICommand command = default!;
        int errorCode = 9998;
        ServiceResult<GuidResult> result;
        var activeStage = ActorRuntimeMetrics.ValidationStage;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                command = ParseMessage(_context!, message);
            }
            finally
            {
                // A materialized command no longer needs the serialized pooled payload.
                message.ReleasePayload();
            }

            /// get any existing error code from the message info...
            errorCode = command.ErrorCode;

            // CommandId is the audit reservation key. Reject an invalid envelope before
            // the audit boundary; derived validation maps retain the same visible rule
            // so their complete command contract remains explicit and directly testable.
            if (command.CommandId == Guid.Empty)
            {
                result = new ServiceFailed<GuidResult>(
                    errorCode,
                    $"{command.CommandName}.CommandId is empty");
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
                activeStage = ActorRuntimeMetrics.DeduplicationStage;
                var stageStarted = ActorRuntimeMetrics.StartStage();
                bool accepted;
                try
                {
                    var reservation = await _commandAuditLogger!
                        .TryReserveAsync(command, cancellationToken)
                        .ConfigureAwait(false);
                    accepted = reservation.Accepted;
                }
                finally
                {
                    ActorRuntimeMetrics.RecordStage(stageStarted, activeStage, ActorType.Command);
                }

                if (!accepted)
                {
                    ActorRuntimeMetrics.DuplicateCommands.Add(1);
                    result = new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
                }
                else
                {
                    var validationErrors = GetCommandValidationErrors(command);
                    if (validationErrors is { Count: > 0 })
                    {
                        result = new ServiceFailed<GuidResult>(
                            command.ErrorCode,
                            string.Join(Environment.NewLine,
                                validationErrors.Select(error => error.ErrorMessage)));
                    }
                    else
                    {
                        /// check if the message is a command and validate it...
                        cancellationToken.ThrowIfCancellationRequested();
                        activeStage = ActorRuntimeMetrics.ValidationStage;
                        stageStarted = ActorRuntimeMetrics.StartStage();
                        try
                        {
                            if (cancellationToken.CanBeCanceled)
                                await OnValidateAsync(_context!, threadId, command, cancellationToken);
                            else
                                await OnValidateAsync(_context!, threadId, command);
                        }
                        finally
                        {
                            ActorRuntimeMetrics.RecordStage(stageStarted, activeStage, ActorType.Command);
                        }

                        /// load the current state, process the message, and save the updated state...
                        cancellationToken.ThrowIfCancellationRequested();
                        activeStage = ActorRuntimeMetrics.ReplayStage;
                        stageStarted = ActorRuntimeMetrics.StartStage();
                        IActorState state;
                        try
                        {
                            state = cancellationToken.CanBeCanceled
                                ? await OnLoadStateAsync(_context!, threadId, command, cancellationToken)
                                : await OnLoadStateAsync(_context!, threadId, command);
                        }
                        finally
                        {
                            ActorRuntimeMetrics.RecordStage(stageStarted, activeStage, ActorType.Command);
                        }
                        state?.Id = threadId;

                        /// process the message...
                        cancellationToken.ThrowIfCancellationRequested();
                        activeStage = ActorRuntimeMetrics.ExecutionStage;
                        stageStarted = ActorRuntimeMetrics.StartStage();
                        try
                        {
                            result = cancellationToken.CanBeCanceled
                                ? await ReceiveAsync(_context!, state!, command, cancellationToken)
                                : await ReceiveAsync(_context!, state!, command);
                        }
                        finally
                        {
                            ActorRuntimeMetrics.RecordStage(stageStarted, activeStage, ActorType.Command);
                        }

                        /// save the updated state...
                        activeStage = ActorRuntimeMetrics.PersistenceStage;
                        stageStarted = ActorRuntimeMetrics.StartStage();
                        try
                        {
                            await OnSaveStateAsync(_context!, threadId, state!, command);
                        }
                        finally
                        {
                            ActorRuntimeMetrics.RecordStage(stageStarted, activeStage, ActorType.Command);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ActorRuntimeMetrics.RecordStageFailure(activeStage, ActorType.Command);
            result = await OnExceptionAsync(_context!, threadId, command, ex);
        }

        /// reply with the result...
        activeStage = ActorRuntimeMetrics.ReplyStage;
        var replyStarted = ActorRuntimeMetrics.StartStage();
        try
        {
            await message.ReplyAsync(result);
        }
        catch
        {
            ActorRuntimeMetrics.RecordStageFailure(activeStage, ActorType.Command);
            throw;
        }
        finally
        {
            ActorRuntimeMetrics.RecordStage(replyStarted, activeStage, ActorType.Command);
        }
    }

    // Explicit interface implementations forwarding to protected hooks.
    ValueTask ICommandActor<TActor>.OnStartup(ICommandActorContext<TActor> context) => OnStartup(context);
    ValueTask ICommandActor<TActor>.OnShutdown(ICommandActorContext<TActor> context) => OnShutdown(context);
    ValueTask<ServiceResult<GuidResult>> ICommandActor<TActor>.ReceiveAsync(ICommandActorContext<TActor> context, IActorState state, ICommand command) => ReceiveAsync(context, state, command);
    ValueTask ICommandActor<TActor>.OnValidateAsync(ICommandActorContext<TActor> context, ActorThreadId threadId, ICommand command) => OnValidateAsync(context, threadId, command);
    ValueTask<IActorState> ICommandActor<TActor>.OnLoadStateAsync(ICommandActorContext<TActor> context, ActorThreadId threadId, ICommand command) => OnLoadStateAsync(context, threadId, command);
    ValueTask ICommandActor<TActor>.OnSaveStateAsync(ICommandActorContext<TActor> context, ActorThreadId threadId, IActorState state, ICommand command) => OnSaveStateAsync(context, threadId, state, command);
    ValueTask<ServiceResult<GuidResult>> ICommandActor<TActor>.OnExceptionAsync(ICommandActorContext<TActor> context, ActorThreadId threadId, ICommand command, Exception ex) => OnExceptionAsync(context, threadId, command, ex);

    ValueTask ICommandActor.OnStartup(ICommandActorContext context) => OnStartup(RequireTypedContext(context));
    ValueTask ICommandActor.OnShutdown(ICommandActorContext context) => OnShutdown(RequireTypedContext(context));
    ValueTask<ServiceResult<GuidResult>> ICommandActor.ReceiveAsync(ICommandActorContext context, IActorState state, ICommand command) => ReceiveAsync(RequireTypedContext(context), state, command);
    ValueTask ICommandActor.OnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command) => OnValidateAsync(RequireTypedContext(context), threadId, command);
    ValueTask<IActorState> ICommandActor.OnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command) => OnLoadStateAsync(RequireTypedContext(context), threadId, command);
    ValueTask ICommandActor.OnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand command) => OnSaveStateAsync(RequireTypedContext(context), threadId, state, command);
    ValueTask<ServiceResult<GuidResult>> ICommandActor.OnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception ex) => OnExceptionAsync(RequireTypedContext(context), threadId, command, ex);

    static ICommandActorContext<TActor> RequireTypedContext(ICommandActorContext context)
        => context as ICommandActorContext<TActor>
            ?? throw new ArgumentException(
                $"The context must implement {typeof(ICommandActorContext<TActor>).Name}.",
                nameof(context));

    // Protected hooks for derived classes
    protected abstract ICommand ParseMessage(ICommandActorContext<TActor> context, IActorMessage message);

    /// <summary>
    /// Returns routine ingress validation errors without using exceptions as control flow.
    /// A null result retains the legacy throwing validation hook for actors not yet migrated.
    /// </summary>
    protected virtual IReadOnlyList<ValidationError>? GetCommandValidationErrors(ICommand command) => null;

    /// <summary>
    /// Resolves a command parser from an actor-owned verb map and materializes the command.
    /// </summary>
    protected ICommand ParseMappedCommand(
        ICommandActorContext<TActor> context,
        IActorMessage message,
        IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> parseMap)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(parseMap);

        var subject = message.Subject;
        if (subject.ActorType != ActorType.Command
            || !string.Equals(subject.Name, Id.Name, StringComparison.Ordinal)
            || !parseMap.TryGetValue(subject.Verb, out var parser))
            throw new InvalidOperationException(
                $"Unable to resolve {Id.Name} command from message: {subject}");

        return parser(message)
            ?? throw new InvalidOperationException(
                $"Parser for {Id.Name}.{subject.Verb} returned no command.");
    }

    /// <summary>
    /// Resolves a command receive handler by the command's exact concrete CLR type.
    /// </summary>
    protected THandler ResolveMappedCommandHandler<THandler>(
        ICommand command,
        IReadOnlyDictionary<Type, THandler> receiveMap)
        where THandler : Delegate
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(receiveMap);

        if (!receiveMap.TryGetValue(command.GetType(), out var handler))
            throw new InvalidOperationException(
                $"Unable to resolve {Id.Name} command from message: {command.Subject}");

        return handler;
    }

    /// <summary>
    /// Resolves exact-type command validation, aggregates every deterministic ingress error,
    /// and throws one <see cref="CommandValidationException"/> when validation fails.
    /// </summary>
    protected void ValidateMappedCommand(
        ICommand command,
        IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> validationMap)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(validationMap);

        if (!validationMap.TryGetValue(command.GetType(), out var validator))
            throw new InvalidOperationException(
                $"Unable to validate {Id.Name} commands from message: {command.Subject}");

        var errors = validator(command)
            ?? throw new InvalidOperationException(
                $"Validator for {command.GetType().Name} returned no error collection.");

        if (errors.Count > 0)
            throw new CommandValidationException(
                command.ErrorCode,
                string.Join(Environment.NewLine, errors.Select(error => error.ErrorMessage))
                + Environment.NewLine);
    }

    /// <summary>
    /// Compatibility entry point for existing command actor tests during the staged mailbox migration.
    /// Runtime command ingress uses <see cref="IActorMessage"/> directly.
    /// </summary>
    protected ICommand ParseMessage(ICommandActorContext<TActor> context, in NatsMsg<byte[]> message)
        => ParseMessage(context, new LegacyNatsActorMessage(message));
    protected virtual ValueTask OnStartup(ICommandActorContext<TActor> context) => ValueTask.CompletedTask;
    protected virtual ValueTask OnStartup(
        ICommandActorContext<TActor> context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnStartup(context);
    }
    protected virtual ValueTask OnShutdown(ICommandActorContext<TActor> context) => ValueTask.CompletedTask;
    protected abstract ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<TActor> context, IActorState state, ICommand command);
    protected virtual ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext<TActor> context,
        IActorState state,
        ICommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ReceiveAsync(context, state, command);
    }
    protected virtual ValueTask OnValidateAsync(ICommandActorContext<TActor> context, ActorThreadId threadId, ICommand command) => ValueTask.CompletedTask;
    protected virtual ValueTask OnValidateAsync(
        ICommandActorContext<TActor> context,
        ActorThreadId threadId,
        ICommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnValidateAsync(context, threadId, command);
    }
    protected virtual ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<TActor> context, ActorThreadId threadId, ICommand command )
    {
        return ValueTask.FromResult<IActorState>(default!);
    }

    protected virtual ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext<TActor> context,
        ActorThreadId threadId,
        ICommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnLoadStateAsync(context, threadId, command);
    }

    protected virtual ValueTask OnSaveStateAsync(ICommandActorContext<TActor> context, ActorThreadId threadId, IActorState state, ICommand command)
    {
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask OnSaveStateAsync(
        ICommandActorContext<TActor> context,
        ActorThreadId threadId,
        IActorState state,
        ICommand command,
        CancellationToken cancellationToken)
        => OnSaveStateAsync(context, threadId, state, command);

    protected abstract ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<TActor> context, ActorThreadId threadId, ICommand command, Exception ex);

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
