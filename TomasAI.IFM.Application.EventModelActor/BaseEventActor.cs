using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Threading;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.EventModelActor;

/// <summary>
/// Base implementation of an event-driven actor that handles events asynchronously with mailbox-driven processing.
/// </summary>
/// <remarks>
/// Mirrors the pattern used by <see cref="BaseEventSourceCommandActor{TActor}"/> but targets the <see cref="IEventActor{TActor}"/> contract.
/// Provides lifecycle hooks (startup/shutdown), message handling, validation, state load/save, and exception handling.
/// </remarks>
/// <typeparam name="TActor">The actor type implementing <see cref="IEventActor{TActor}"/>.</typeparam>
/// <param name="actorContext">The closed-generic event context owned by the actor for its entire lifetime.</param>
/// <param name="logger">The logger used to record operational and diagnostic information.</param>
public abstract class BaseEventActor<TActor>(
    IEventActorContext<TActor> actorContext,
    ILogger logger)
    : IEventActor<TActor>, IReplaceableRealtimeActor where TActor : IActor
{
    readonly IEventActorContext<TActor> _context = IsArgumentNull.Set(actorContext);
    readonly ActorMailboxId _actorId = IsArgumentNull.Set(actorContext).ActorId;
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    IActorSupervisor _supervisor;
    int _lifecycle;

    /// <inheritdoc />
    public RealtimeActorGeneration RealtimeGeneration { get; } =
        (actorContext as EventActorContext)?.RealtimeGeneration ?? new();

    /// <inheritdoc />
    public void RetireRealtimeGeneration()
    {
        if (Id.ActorType != ActorType.Realtime)
            throw new InvalidOperationException("Only disposable realtime actors can be retired without draining.");
        RealtimeGeneration.Retire();
        Volatile.Write(ref _lifecycle, 0);
    }

    // IActor properties
    public ActorMailboxId Id => _actorId;
    /// <summary>
    /// Gets the closed-generic event context retained for the lifetime of this actor.
    /// </summary>
    protected IEventActorContext<TActor> Context => _context;
    public IActorMailbox Mailbox { get; private set; }
    public bool IsRunning
    {
        get => Volatile.Read(ref _lifecycle) == 2;
        protected set => Volatile.Write(ref _lifecycle, value ? 2 : 0);
    }
    public bool IsParent { get; protected set; }

    IActorProducer? _startedCoreProducer;
    IJSActorProducer? _startedJetStreamProducer;

    /// <summary>
    /// Starts the actor by wiring up producer/consumer and initializing the event actor context.
    /// </summary>
    public async ValueTask StartAsync(IActorSupervisor supervisor)
        => await StartAsync(supervisor, CancellationToken.None).ConfigureAwait(false);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the ordered actor lifecycle state machine in one method so cancellation, commit, acknowledgement and cleanup ordering can be audited together.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2016", Justification = "Legacy hooks are called only when cancellation cannot occur; failed-startup cleanup deliberately uses unconditional stop.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/channel boundary isolates arbitrary handler failures, records the failed outcome, and keeps processing other messages.")]
    public async ValueTask StartAsync(IActorSupervisor supervisor, CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(supervisor);
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref _lifecycle, 1, 0) != 0)
            return;
        IActorProducer? coreProducer = null;
        IJSActorProducer? jetStreamProducer = null;
        try
        {
            _supervisor = supervisor;
            Mailbox = supervisor.CreateMailbox(_actorId);
            switch (_actorId.ActorType.GetDeliveryType())
            {
                case ActorDeliveryType.NatsCore:
                    coreProducer = _supervisor.GetProducer(_actorId);
                    await coreProducer.StartAsync(_actorId, cancellationToken).ConfigureAwait(false);
                    _startedCoreProducer = coreProducer;
                    break;
                case ActorDeliveryType.NatsJetStream:
                    jetStreamProducer = _supervisor.GetJSProducer(_actorId);
                    await jetStreamProducer.StartAsync(_actorId, cancellationToken).ConfigureAwait(false);
                    _startedJetStreamProducer = jetStreamProducer;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Actor type '{_actorId.ActorType}' does not define a delivery transport.");
            }
            var serviceId = typeof(TActor).Name;
            _logger.LogInformationEvent(serviceId, "Started {MailboxId} producer.", _actorId);
            using var generationScope = Id.ActorType == ActorType.Realtime ? RealtimeGeneration.Enter() : null;
            await OnStartup(_context, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _lifecycle, 2);
        }
        catch (Exception startupFailure)
        {
            var primaryFailureId = startupFailure is OperationCanceledException && cancellationToken.IsCancellationRequested
                ? Guid.Empty
                : _context.SupervisorRuntime?.RecordFailure(
                    Id, new(Id.ActorType, Id.Name, "lifecycle"), "Start",
                    ActorFailureStage.Startup, startupFailure) ?? Guid.Empty;
            SupervisorRuntimeContext.MarkRecorded(startupFailure, primaryFailureId);
            if (coreProducer is not null)
            {
                try { await coreProducer.StopAsync().ConfigureAwait(false); }
                catch (Exception cleanupException)
                {
                    _context.SupervisorRuntime?.RecordFailure(
                        Id, new(Id.ActorType, Id.Name, "lifecycle"), "Start",
                        ActorFailureStage.Cleanup, cleanupException,
                        primaryFailureId == Guid.Empty ? null : primaryFailureId,
                        ActorMessageOutcomeType.HandledFailure);
                    SharedActorDiagnosticLog.Write5(_logger, cleanupException, _actorId);
                }
            }
            if (jetStreamProducer is not null)
            {
                try { await jetStreamProducer.StopAsync().ConfigureAwait(false); }
                catch (Exception cleanupException)
                {
                    _context.SupervisorRuntime?.RecordFailure(
                        Id, new(Id.ActorType, Id.Name, "lifecycle"), "Start",
                        ActorFailureStage.Cleanup, cleanupException,
                        primaryFailureId == Guid.Empty ? null : primaryFailureId,
                        ActorMessageOutcomeType.HandledFailure);
                    SharedActorDiagnosticLog.Write7(_logger, cleanupException, _actorId);
                }
            }
            Volatile.Write(ref _lifecycle, 0);
            throw;
        }
    }

    /// <summary>
    /// Stops the actor and tears down producer/consumer resources.
    /// </summary>
    public async ValueTask StopAsync()
        => await StopAsync(CancellationToken.None).ConfigureAwait(false);

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _lifecycle, 3, 2) != 2)
            return;
        try
        {
            await ActorLifecycleGuard.StopAsync(
                _context.SupervisorRuntime,
                _actorId,
                async () =>
                {
                    switch (_actorId.ActorType.GetDeliveryType())
                    {
                        case ActorDeliveryType.NatsCore:
                            if (_startedCoreProducer is not null)
                                await _startedCoreProducer.StopAsync().ConfigureAwait(false);
                            break;
                        case ActorDeliveryType.NatsJetStream:
                            if (_startedJetStreamProducer is not null)
                                await _startedJetStreamProducer.StopAsync().ConfigureAwait(false);
                            break;
                        default:
                            throw new InvalidOperationException(
                                $"Actor type '{_actorId.ActorType}' does not define a delivery transport.");
                    }
                    SharedActorDiagnosticLog.Write6(_logger, null, _actorId);
                },
                () => OnShutdown(_context!),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _startedCoreProducer = null;
            _startedJetStreamProducer = null;
            Volatile.Write(ref _lifecycle, 0);
        }
    }

    /// <summary>
    /// Handles an incoming message by validating and receiving.
    /// </summary>
    public ValueTask HandleMessageAsync(IActorMessage message)
        
    {
        ArgumentNullException.ThrowIfNull(message);
        return HandleMessageAsync(message, message.Subject.ThreadId, CancellationToken.None);
    }

    /// <summary>
    /// Handles an incoming message using a pre-resolved thread identifier, avoiding redundant subject parsing.
    /// </summary>
    /// <param name="message">The message to be processed.</param>
    /// <param name="threadId">The pre-resolved thread identifier from the caller.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId)
        => await HandleMessageAsync(message, threadId, CancellationToken.None).ConfigureAwait(false);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the ordered actor lifecycle state machine in one method so cancellation, commit, acknowledgement and cleanup ordering can be audited together.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/channel boundary isolates arbitrary handler failures, records the failed outcome, and keeps processing other messages.")]
    public async ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var generationScope = Id.ActorType == ActorType.Realtime ? RealtimeGeneration.Enter() : null;
        IEvent? @event = null;
        var activeStage = ActorRuntimeMetrics.ParsingStage;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // check if we can handle this event...
            if (_context is null)
            {
                message.ReleasePayload();
                return;
            }


            try
            {
                @event = ParseMessage(_context!, message);
            }
            finally
            {
                // Every fan-out branch releases its reference immediately after
                // materializing its own typed event instance.
                message.ReleasePayload();
            }
            if (@event == null)
                return;

            /// Check if the message is a command and validate it
            cancellationToken.ThrowIfCancellationRequested();
            activeStage = ActorRuntimeMetrics.ValidationStage;
            var stageStarted = ActorRuntimeMetrics.StartStage();
            try
            {
                await OnValidateAsync(_context!, threadId, @event, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ActorRuntimeMetrics.RecordStage(stageStarted, activeStage, _actorId.ActorType);
            }

            // Process message
            cancellationToken.ThrowIfCancellationRequested();
            activeStage = ActorRuntimeMetrics.ExecutionStage;
            stageStarted = ActorRuntimeMetrics.StartStage();
            try
            {
                await ReceiveAsync(_context!, @event, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ActorRuntimeMetrics.RecordStage(stageStarted, activeStage, _actorId.ActorType);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ActorRuntimeMetrics.RecordStageFailure(activeStage, _actorId.ActorType);
            var primaryFailureId = ActorOperationalMetrics.RecordHandledFailure(
                Mailbox, _context.SupervisorRuntime, threadId, message.Subject.Verb,
                ActorOperationalMetrics.MapStage(activeStage), ex);
            try
            {
                if (@event is null)
                    HandleEventParseFailure(message, ex);
                else
                    await OnExceptionAsync(_context!, threadId, @event, ex).ConfigureAwait(false);
            }
            catch (Exception exceptionHandlerFailure)
            {
                _context.SupervisorRuntime?.RecordFailure(
                    Id, threadId, message.Subject.Verb, ActorFailureStage.ExceptionHandling,
                    exceptionHandlerFailure, primaryFailureId == Guid.Empty ? null : primaryFailureId);
                SharedActorDiagnosticLog.Write8(_logger, exceptionHandlerFailure, Id, threadId, activeStage, ex.GetType().FullName, ex.Message);
            }
        }
    }

    // Explicit interface implementations forwarding to protected hooks
    ValueTask IEventActor<TActor>.OnStartup(IEventActorContext<TActor> context) => OnStartup(context);
    ValueTask IEventActor<TActor>.OnShutdown(IEventActorContext<TActor> context) => OnShutdown(context);
    ValueTask IEventActor<TActor>.ReceiveAsync(IEventActorContext<TActor> context, IEvent @event) => ReceiveAsync(context, @event);
    ValueTask IEventActor<TActor>.OnValidateAsync(IEventActorContext<TActor> context, ActorThreadId threadId, IEvent @event) => OnValidateAsync(context, threadId, @event);
    ValueTask IEventActor<TActor>.OnExceptionAsync(IEventActorContext<TActor> context, ActorThreadId threadId, IEvent @event, Exception ex) => OnExceptionAsync(context, threadId, @event, ex);

    // Protected hooks for derived classes
    protected abstract IEvent ParseMessage(IEventActorContext<TActor> context, IActorMessage message);

    /// <summary>
    /// Resolves an event parser from an actor-owned verb map and materializes the event.
    /// </summary>
    /// <remarks>
    /// Event messages for another actor or an unsupported verb are ignored because EventActor
    /// delivery may fan out one owned message branch to multiple consumers. Once a verb is
    /// registered, however, its parser must materialize a concrete event.
    /// </remarks>
    protected IEvent ParseMappedEvent(
        IEventActorContext<TActor> context,
        IActorMessage message,
        IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> parseMap)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(parseMap);

        var subject = message.Subject;
        if (subject.ActorType != ActorType.Event
            || !string.Equals(subject.Name, Id.Name, StringComparison.Ordinal)
            || !parseMap.TryGetValue(subject.Verb, out var parser))
            return default!;

        var @event = parser(message)
            ?? throw new InvalidOperationException(
                $"Parser for {Id.Name}.{subject.Verb} returned no event.");
        @event.CheckForEmptyCommandId();
        return @event;
    }

    /// <summary>
    /// Resolves a realtime-event parser from an actor-owned verb map and materializes the event.
    /// </summary>
    /// <remarks>
    /// Realtime messages are one-way Core NATS observations. They use the same event contract and
    /// exact-type dispatch model as durable EventActors, but private realtime events are not
    /// required to originate from a command and therefore are not required to carry a command ID.
    /// Messages for another realtime actor or an unsupported verb are ignored because realtime
    /// delivery may fan out one owned message branch to multiple routed consumers.
    /// </remarks>
    protected IEvent ParseMappedRealtimeEvent(
        IEventActorContext<TActor> context,
        IActorMessage message,
        IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> parseMap)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(parseMap);

        var subject = message.Subject;
        if (subject.ActorType != ActorType.Realtime
            || !string.Equals(subject.Name, Id.Name, StringComparison.Ordinal)
            || !parseMap.TryGetValue(subject.Verb, out var parser))
            return default!;

        return parser(message)
            ?? throw new InvalidOperationException(
                $"Parser for {Id.Name}.{subject.Verb} returned no realtime event.");
    }

    /// <summary>
    /// Resolves an event receive handler by the event's exact concrete CLR type. Missing event
    /// handlers intentionally resolve to a no-op; event delivery may fan out to actors that do not
    /// consume every materialized event type.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Preserve the instance member called by existing derived actor/validation consumers.")]
    protected THandler ResolveMappedEventHandler<THandler>(
        IEvent @event,
        IReadOnlyDictionary<Type, THandler> receiveMap)
        where THandler : Delegate
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(receiveMap);

        return receiveMap.TryGetValue(@event.GetType(), out var handler)
            ? handler
            : NoOpEventHandler<THandler>.Instance;
    }

    static class NoOpEventHandler<THandler> where THandler : Delegate
    {
        internal static readonly THandler Instance = Create();

        static THandler Create()
        {
            var invoke = typeof(THandler).GetMethod(nameof(Action.Invoke))
                ?? throw new InvalidOperationException($"{typeof(THandler).FullName} is not invokable.");
            var parameters = invoke.GetParameters()
                .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name))
                .ToArray();
            Expression body;
            if (invoke.ReturnType == typeof(void)) body = Expression.Empty();
            else if (invoke.ReturnType == typeof(Task))
                body = Expression.Property(null, typeof(Task), nameof(Task.CompletedTask));
            else if (invoke.ReturnType.IsGenericType && invoke.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                body = Expression.Call(typeof(Task), nameof(Task.FromResult),
                    [invoke.ReturnType.GetGenericArguments()[0]],
                    Expression.Default(invoke.ReturnType.GetGenericArguments()[0]));
            else body = Expression.Default(invoke.ReturnType);
            return Expression.Lambda<THandler>(body, parameters).Compile();
        }
    }

    void HandleEventParseFailure(IActorMessage message, Exception exception) =>
        SharedActorDiagnosticLog.Write9(_logger, exception, _actorId.ActorType, Id, message.Subject);

    protected virtual ValueTask OnStartup(IEventActorContext<TActor> context) => ValueTask.CompletedTask;
    protected virtual ValueTask OnStartup(
        IEventActorContext<TActor> context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnStartup(context);
    }
    protected virtual ValueTask OnShutdown(IEventActorContext<TActor> context) => ValueTask.CompletedTask;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
    protected abstract ValueTask ReceiveAsync(IEventActorContext<TActor> context, IEvent @event);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
    protected virtual ValueTask ReceiveAsync(IEventActorContext<TActor> context, IEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ReceiveAsync(context, @event);
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
    protected virtual ValueTask OnValidateAsync(IEventActorContext<TActor> context, ActorThreadId threadId, IEvent @event) => ValueTask.CompletedTask;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
    protected virtual ValueTask OnValidateAsync(IEventActorContext<TActor> context, ActorThreadId threadId, IEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnValidateAsync(context, threadId, @event);
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
    protected abstract ValueTask OnExceptionAsync(IEventActorContext<TActor> context, ActorThreadId threadId, IEvent @event, Exception ex);
}
