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

    // IActor properties
    public ActorMailboxId Id => _actorId;
    public IActorMailbox Mailbox { get; } = supervisor.CreateMailbox(actorId)!;
    public bool IsRunning { get; protected set; }
    public bool IsParent { get; protected set; }

    /// <summary>
    /// Starts the actor by wiring up producer/consumer and initializing the event actor context.
    /// </summary>
    public async ValueTask StartAsync(IActorSupervisor supervisorArg)
    {
        IsArgumentNull.Check(supervisorArg);

        if (IsRunning)
            return;

        if (Mailbox is null)
            throw new InvalidOperationException("Mailbox must be set before starting the actor.");

        // start producer
        var producer = _supervisor.GetJSProducer(_actorId);
        await producer.StartAsync(_actorId).ConfigureAwait(false);
        _serviceId = typeof(TActor).Name;
        _logger.LogInformationEvent(_serviceId, "Started {MailboxId} producer.", _actorId);

        // initialize event actor context
        _context = new EventActorContext(_supervisor, _actorId);
        await OnStartup(_context).ConfigureAwait(false);
        IsRunning = true;
    }

    /// <summary>
    /// Stops the actor and tears down producer/consumer resources.
    /// </summary>
    public async ValueTask StopAsync()
    {
        if (!IsRunning)
            return;

        var producer = _supervisor.GetProducer(_actorId);
        await producer.StopAsync().ConfigureAwait(false);
        _logger.LogInformation("Stopped {MailboxId} producer.", _actorId);

        await _supervisor.StopAsync(_actorId).ConfigureAwait(false);
        await OnShutdown(_context!).ConfigureAwait(false);

        IsRunning = false;
    }

    /// <summary>
    /// Handles an incoming message by validating and receiving.
    /// </summary>
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
        IEvent @event = default! ;
        try
        {
            // check if we can handle this event...
            if (_context is null)
                return;


            @event =ParseMessage( _context!, message);
            if (@event == null)
                return;

            /// Check if the message is a command and validate it
            await OnValidateAsync(_context!, threadId, @event);

            // Process message
            await ReceiveAsync(_context!, @event);
        }
        catch (Exception ex)
        {
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
    protected abstract IEvent ParseMessage(IEventActorContext context, NatsMsg<byte[]> message);
    protected virtual ValueTask OnStartup(IEventActorContext context) => ValueTask.CompletedTask;
    protected virtual ValueTask OnShutdown(IEventActorContext context) => ValueTask.CompletedTask;
    protected abstract ValueTask ReceiveAsync(IEventActorContext context, IEvent @event);
    protected virtual ValueTask OnValidateAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event) => ValueTask.CompletedTask;
    protected abstract ValueTask OnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex);
}
