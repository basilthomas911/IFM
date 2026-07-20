using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides a base implementation for a denormalizer actor that processes messages, manages state, and coordinates
/// event handling within an actor-based system.
/// </summary>
/// <remarks>This abstract class defines the core lifecycle and message-handling logic for denormalizer actors,
/// including startup, shutdown, and error handling. Derived classes should implement the abstract methods to provide
/// custom message parsing, state updates, and exception handling. The class ensures that actor resources are properly
/// initialized and released, and provides hooks for extending behavior in specialized denormalizer scenarios.</remarks>
/// <typeparam name="TActor">The type of actor managed by this denormalizer. Must implement <see cref="IActor"/>.</typeparam>
/// <param name="logger">The logger used to record operational and diagnostic information for the actor.</param>
/// <param name="actorId">The unique identifier for the actor's mailbox, used to route messages and manage actor state.</param>
public abstract class BaseDenormalizerActor<TActor>(ILogger logger, ActorMailboxId actorId)
    : IDenormalizerActor<TActor> where TActor : IActor
{
    readonly ActorMailboxId _actorId = IsArgumentNull.Set(actorId);
    readonly ILogger _logger = IsArgumentNull.Set(logger);
    string _serviceId = string.Empty;
    IDenormalizerActorContext _context;
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
        _context = supervisor.CreateDenormalizerActorContext(actorId);
        await OnStartup(_context);
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
        await OnShutdown(_context!);

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
        IEvent @event = default!;
        try
        {
            @event = ParseMessage(_context!, message);

            /// process the message and get the result...
            await ReceiveAsync(_context!, threadId, @event);
        }
        catch (Exception ex)
        {
            await OnExceptionAsync(_context!, threadId, @event, ex);
        }
    }

    // Explicit interface implementations forwarding to protected hooks
    protected abstract IEvent ParseMessage(IDenormalizerActorContext context, NatsMsg<byte[]> message);
    ValueTask IDenormalizerActor<TActor>.OnStartup(IDenormalizerActorContext context) => OnStartup(context);
    ValueTask IDenormalizerActor<TActor>.OnShutdown(IDenormalizerActorContext context) => OnShutdown(context);
    ValueTask IDenormalizerActor<TActor>.ReceiveAsync(IDenormalizerActorContext context, ActorThreadId threadId, IEvent @event) => ReceiveAsync(context, threadId, @event);
    ValueTask IDenormalizerActor<TActor>.OnExceptionAsync(IDenormalizerActorContext context, ActorThreadId threadId,IEvent @event, Exception ex) => OnExceptionAsync(context, threadId, @event  , ex);

    // Protected hooks for derived classes
    protected virtual ValueTask OnStartup(IDenormalizerActorContext context) => ValueTask.CompletedTask;
    protected virtual ValueTask OnShutdown(IDenormalizerActorContext context) => ValueTask.CompletedTask;
    protected abstract ValueTask ReceiveAsync(IDenormalizerActorContext context, ActorThreadId threadId, IEvent @event);
    protected abstract ValueTask OnExceptionAsync(IDenormalizerActorContext context, ActorThreadId threadId, IEvent @event, Exception ex);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dernomalizeEvent"></param>
    /// <param name="denormalizerAction"></param>
    /// <param name="postDenormalizeEvent"></param>
    /// <returns></returns>
    protected async ValueTask<bool> UpdateReadModelAsync<TEvent, TComplete, TFail, TEntityId>(IDenormalizerActorContext context, TEvent dernomalizeEvent, Func<ValueTask> denormalizerAction, bool postDenormalizeEvent = true)
        where TEvent :  class,IEvent<TEntityId>
        where TComplete : class, ICompleteEvent<TEntityId>
        where TFail : class, IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        try
        {
            dernomalizeEvent.CheckForEmptyCommandId();
            if (postDenormalizeEvent)
            {
                EventInitHelper.SetProperty(dernomalizeEvent, nameof(IEvent.Subject), new ActorSubject(ActorType.Event, dernomalizeEvent.Subject.Name
                    , dernomalizeEvent.Subject.Verb, dernomalizeEvent.EntityId.Format()));
                await context.SendAsync<TEvent, TEntityId>(dernomalizeEvent);
            }
            await denormalizerAction();
            var completedEvent = dernomalizeEvent.ToCompleteEvent<TComplete, TEntityId>() as TComplete;
            if (completedEvent is not null)
            {
                await context.SendAsync<TComplete, TEntityId>(completedEvent);
            }
        }
        catch(Exception ex)
        {
            var failedEvent = dernomalizeEvent.ToFailEvent<TFail, TEntityId>(ex) as TFail;
            await context.SendAsync<TFail, TEntityId>(failedEvent!);
        }
        return true;
    }

    /// <summary>
    /// only post event with no read model update
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    protected async ValueTask<bool> PostEventAsync<TEvent, TEntityId>(IDenormalizerActorContext context, TEvent e)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        try
        {
            e.CheckForEmptyCommandId();
            EventInitHelper.SetProperty(e, nameof(IEvent.Subject), new ActorSubject(ActorType.Event, e.Subject.Name.Replace("Denormalizer", "Event")
                , e.Subject.Verb, e.EntityId.Format()));
            await context.SendAsync<TEvent, TEntityId>(e);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "PostEventAsync: failed to post event from denormalizer {EventName} due to {ErrorMessage}", e.GetType().Name, ex.Message);
        }
        return true;
    }




}
