using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Base implementation of a query-driven actor that processes messages asynchronously with mailbox-driven processing.
/// </summary>
/// <remarks>
/// Mirrors the pattern used by <see cref="BaseEventSourceCommandActor{TActor}"/> but targets the <see cref="IQueryActor{TActor}"/> contract.
/// Provides lifecycle hooks (startup/shutdown), message handling, validation, state load/save, and exception handling.
/// </remarks>
/// <typeparam name="TActor">The actor type implementing <see cref="IQueryActor{TActor}"/>.</typeparam>
public abstract class BaseQueryActor<TActor>( ILogger logger, ActorMailboxId actorId)
    : IQueryActor<TActor> where TActor : IActor
{
    readonly ActorMailboxId _actorId = IsArgumentNull.Set(actorId);
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    string _serviceId = string.Empty;

    IQueryActorContext? _context;
    IActorSupervisor _supervisor;

    // IActor properties
    public ActorMailboxId Id => _actorId;
    protected IQuery Query { get; set; }
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
        _context = supervisor.CreateQueryActorContext(actorId);
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
    /// Handles an incoming message for the actor, performing validation and message processing.
    /// </summary>
    /// <remarks>This method validates the message to ensure it is intended for the current actor and processes
    /// the message. If an exception occurs during processing, it is handled by invoking the exception handler.</remarks>
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
        IQuery query = default!;
        string verb = default!;
        try
        {
            query = ParseMessage(_context!, message);

            /// check if the message is a command and validate it
            await OnValidateAsync(_context!, query);

            /// process the message
            await ReceiveAsync(_context!, query);
        }
        catch (Exception ex)
        {
            verb = message.Subject.ExtractVerb();
            await OnExceptionAsync(_context!, threadId, query, verb, ex);
        }
    }

    // Explicit interface implementations forwarding to protected hooks
    ValueTask IQueryActor<TActor>.OnStartup(IQueryActorContext context) => OnStartup(context);
    ValueTask IQueryActor<TActor>.OnShutdown(IQueryActorContext context) => OnShutdown(context);
    ValueTask IQueryActor<TActor>.ReceiveAsync(IQueryActorContext context, IQuery query) => ReceiveAsync(context, query);
    ValueTask IQueryActor<TActor>.OnValidateAsync(IQueryActorContext context, IQuery query) => OnValidateAsync(context, query);
    ValueTask IQueryActor<TActor>.OnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => OnExceptionAsync(context, threadId, query, verb, ex);

    // Protected hooks for derived classes
    protected abstract IQuery ParseMessage(IQueryActorContext context, NatsMsg<byte[]> message);
    protected virtual ValueTask OnStartup(IQueryActorContext context) => ValueTask.CompletedTask;
    protected virtual ValueTask OnShutdown(IQueryActorContext context) => ValueTask.CompletedTask;
    protected abstract ValueTask ReceiveAsync(IQueryActorContext context, IQuery query);
    protected virtual ValueTask OnValidateAsync(IQueryActorContext context, IQuery query) => ValueTask.CompletedTask;
    protected abstract  ValueTask OnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex);
}
