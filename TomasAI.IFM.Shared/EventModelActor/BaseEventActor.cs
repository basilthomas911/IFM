using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using System.Threading;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Base implementation of an event-driven actor that handles events asynchronously with mailbox-driven processing.
/// </summary>
/// <remarks>
/// Mirrors the pattern used by <see cref="BaseEventSourceCommandActor{TActor}"/> but targets the <see cref="IEventActor{TActor}"/> contract.
/// Provides lifecycle hooks (startup/shutdown), message handling, validation, state load/save, and exception handling.
/// </remarks>
/// <typeparam name="TActor">The actor type implementing <see cref="IEventActor{TActor}"/>.</typeparam>
public abstract class BaseEventActor<TActor>(IActorSupervisor supervisor, ILogger logger, ActorMailboxId actorId)
    : IEventActor<TActor> where TActor : IActor
{
    readonly IActorSupervisor _supervisor = IsArgumentNull.Set(supervisor);
    readonly ActorMailboxId _actorId = IsArgumentNull.Set(actorId);
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    IEventActorContext? _context;
    string _serviceId = string.Empty;
    int _lifecycle;

    // IActor properties
    public ActorMailboxId Id => _actorId;
    public IActorMailbox Mailbox { get; } = supervisor.CreateMailbox(actorId)!;
    public bool IsRunning
    {
        get => Volatile.Read(ref _lifecycle) == 2;
        protected set => Volatile.Write(ref _lifecycle, value ? 2 : 0);
    }
    public bool IsParent { get; protected set; }

    /// <summary>
    /// Starts the actor by wiring up producer/consumer and initializing the event actor context.
    /// </summary>
    public async ValueTask StartAsync(IActorSupervisor supervisorArg)
        => await StartAsync(supervisorArg, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask StartAsync(IActorSupervisor supervisorArg, CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(supervisorArg);
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref _lifecycle, 1, 0) != 0)
            return;
        IJSActorProducer? producer = null;
        try
        {
            producer = _supervisor.GetJSProducer(_actorId);
            await producer.StartAsync(_actorId, cancellationToken).ConfigureAwait(false);
            _serviceId = typeof(TActor).Name;
            _logger.LogInformationEvent(_serviceId, "Started {MailboxId} producer.", _actorId);
            _context = new EventActorContext(_supervisor, _actorId);
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
            var producer = _supervisor.GetJSProducer(_actorId);
            // Once shutdown owns the actor lifecycle transition, finish cleanup atomically.
            await producer.StopAsync().ConfigureAwait(false);
            _logger.LogInformation("Stopped {MailboxId} producer.", _actorId);
        }
        finally
        {
            try
            {
                await OnShutdown(_context!).ConfigureAwait(false);
            }
            finally
            {
                Volatile.Write(ref _lifecycle, 0);
            }
        }
    }

    /// <summary>
    /// Handles an incoming message by validating and receiving.
    /// </summary>
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

    public async ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId, CancellationToken cancellationToken)
    {
        IEvent @event = default! ;
        var activeStage = ActorRuntimeMetrics.ValidationStage;
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
                ActorRuntimeMetrics.RecordStage(stageStarted, activeStage, ActorType.Event);
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
                ActorRuntimeMetrics.RecordStage(stageStarted, activeStage, ActorType.Event);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ActorRuntimeMetrics.RecordStageFailure(activeStage, ActorType.Event);
            await OnExceptionAsync(_context!, threadId, @event, ex);
        }
    }

    // Explicit interface implementations forwarding to protected hooks
    ValueTask IEventActor<TActor>.OnStartup(IEventActorContext context) => OnStartup(context);
    ValueTask IEventActor<TActor>.OnShutdown(IEventActorContext context) => OnShutdown(context);
    ValueTask IEventActor<TActor>.ReceiveAsync(IEventActorContext context, IEvent @event) => ReceiveAsync(context, @event);
    ValueTask IEventActor<TActor>.OnValidateAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event) => OnValidateAsync(context, threadId, @event);
    ValueTask IEventActor<TActor>.OnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex) => OnExceptionAsync(context, threadId, @event, ex);

    // Protected hooks for derived classes
    protected abstract IEvent ParseMessage(IEventActorContext context, IActorMessage message);

    /// <summary>
    /// Compatibility entry point for existing event actor tests while the
    /// runtime path uses owned <see cref="IActorMessage"/> branches directly.
    /// </summary>
    protected IEvent ParseMessage(IEventActorContext context, in NatsMsg<byte[]> message)
        => ParseMessage(context, new LegacyNatsActorMessage(message));
    protected virtual ValueTask OnStartup(IEventActorContext context) => ValueTask.CompletedTask;
    protected virtual ValueTask OnStartup(
        IEventActorContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnStartup(context);
    }
    protected virtual ValueTask OnShutdown(IEventActorContext context) => ValueTask.CompletedTask;
    protected abstract ValueTask ReceiveAsync(IEventActorContext context, IEvent @event);
    protected virtual ValueTask ReceiveAsync(IEventActorContext context, IEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ReceiveAsync(context, @event);
    }
    protected virtual ValueTask OnValidateAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event) => ValueTask.CompletedTask;
    protected virtual ValueTask OnValidateAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnValidateAsync(context, threadId, @event);
    }
    protected abstract ValueTask OnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex);
}
