using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents a thread dedicated to managing the execution of an actor within a thread pool.
/// </summary>
/// <remarks>The <see cref="ActorThread"/> class is responsible for scheduling and processing messages for a
/// specific actor. It provides methods to start, stop, and post messages to the actor's thread channel, as well as
/// manage the thread's state. The thread operates asynchronously and ensures proper handling of timeouts, faults, and
/// other operational states.</remarks>
sealed class ActorThread : IActorThread
{
    readonly ILogger? _logger;
    readonly IActorThreadPool _threadPool;
    ActorThreadId _threadId;
    ActorThreadScheduler _threadScheduler;
    TimeSpan _timeout;
    IActor? _actor;
    Timer? _timer;
    volatile ActorThreadState _state;
    Exception? _exception;
    volatile bool _reading;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorThread"/> class, which manages the execution of an
    /// actor on a specific thread within a thread pool.
    /// </summary>
    /// <remarks>This constructor initializes the actor thread in the <see
    /// cref="ActorThreadState.Ready"/> state and sets up an asynchronous event channel for processing actor
    /// messages. The event channel is named based on the actor's mailbox ID.</remarks>
    /// <param name="actor">The actor instance to be managed by this thread. Cannot be <see langword="null"/>.</param>
    /// <param name="threadId">The unique identifier for the thread managing the actor. Cannot be <see langword="null"/>.</param>
    /// <param name="logger">An optional logger for capturing event channel activity. Can be <see langword="null"/>.</param>
    /// <param name="timeout">The maximum duration to wait for operations to complete before timing out.</param>
    public ActorThread(IActorThreadPool threadPool,ILogger logger, TimeSpan timeout)
    {
        _threadPool = IsArgumentNull.Set(threadPool);
        _logger = IsArgumentNull.Set(logger);
        _threadScheduler = new ActorThreadScheduler(OnMessageAsync, _logger);
        _state = ActorThreadState.Ready;
        _timeout = timeout;
        _threadId = default!;
        _reading = false;
    }

    public ActorThreadId Id { get; set; }

    /// <summary>
    /// Posts a message to the actor's thread channel for processing.
    /// </summary>
    /// <remarks>The method returns <see langword="false"/> if the actor is stopped, faulted, or timed
    /// out,  or if an error occurs while posting the message.</remarks>
    /// <param name="message">The message to be posted. Cannot be <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the message was successfully posted; otherwise, <see langword="false"/>.</returns>
    public bool Post(in NatsMsg<byte[]> message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (IsStopped || IsFaulted || IsTimedOut)
            return false;
        return _threadScheduler?.WriteData(message) ?? false;
    }

    public async ValueTask WriteToActorThreadQueueAsync(NatsMsg<byte[]> message, CancellationToken cancellationToken = default)
        => await ValueTask.CompletedTask;

    public async ValueTask WriteToActorThreadQueueAsync(NatsMsg<byte[]> message, ActorSubject subject, CancellationToken cancellationToken = default)
        => await ValueTask.CompletedTask;

    /// <summary>
    /// Starts the actor thread with the specified actor and thread identifier.
    /// </summary>
    /// <param name="actor">The actor to associate with this thread. Cannot be <see langword="null"/>.</param>
    /// <param name="threadId">The identifier of the thread to start. Cannot be <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the thread was successfully started; otherwise, <see langword="false"/>.</returns>
    public bool Start(IActor actor, ActorThreadId threadId)
    {
        Id = threadId;
        if (!IsStarted)
        {
            _actor = IsArgumentNull.Set(actor); 
            _threadId = IsArgumentNull.Set(threadId);
            var threadQueue = actor.Mailbox.ThreadQueues.GetThreadQueue(threadId);
            _threadScheduler.Start(threadQueue);
            _state = ActorThreadState.Started;
            _reading = true;
            ResetTimer();
        }
        return true;
    }

    /// <inheritdoc/>
    public bool Start() => true;

    /// <inheritdoc/>
    public void SignalMessageAvailable(ActorThreadId threadId) { }

    /// <inheritdoc/>
    public void Assign(IActor actor, ActorThreadId threadId)
    {
        if (!IsStarted)
            Start(actor, threadId);
    }

    /// <summary>
    /// Stops the actor thread and releases its resources.
    /// </summary>
    /// <remarks>This method ensures that the actor thread is stopped, its associated timer is disposed,  and
    /// the thread is released back to the thread pool. If the thread is already stopped,  the method has no
    /// effect.</remarks>
    /// <returns><see langword="true"/> to indicate that the stop operation was completed successfully.</returns>
    public bool Stop()
    {
        if (!IsStopped)
        {
            try
            {
                _reading = false;
                _threadScheduler.Stop();
                _actor?.Mailbox.ThreadQueues.ReleaseThreadQueue(_threadId);
                _timer?.Dispose();
            }
            catch { }
            finally { 
                _state = ActorThreadState.Stopped;
                _threadPool.ReleaseThread(_threadId);
            }
        }
        return true;
    }

    // Include _reading in runtime state checks so the field is read and its assignments are meaningful.
    public bool IsRunning => _reading || _state == ActorThreadState.ProcessingMessage || _state == ActorThreadState.WaitingForMessage;
    public bool IsStarted => _reading || _state == ActorThreadState.Started || _state == ActorThreadState.ProcessingMessage || _state == ActorThreadState.WaitingForMessage;
    public bool IsStopped => _state == ActorThreadState.Stopped;
    public bool IsFaulted => _state == ActorThreadState.Faulted;
    public bool IsTimedOut => _state == ActorThreadState.TimedOut;
    public ActorThreadState State => _state;
    public Exception? Exception => _exception;

    /// <summary>
    /// Handles the processing of an incoming actor message asynchronously.
    /// </summary>
    /// <remarks>This method transitions the actor's state to indicate that it is processing a
    /// message, invokes the actor's message handling logic, and then transitions the state back to waiting for
    /// the next message. If an exception occurs during message processing, the actor is set to a faulted
    /// state.</remarks>
    /// <param name="message">The message to be processed by the actor. Cannot be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    async ValueTask OnMessageAsync(NatsMsg<byte[]> message)
    {
        _state = ActorThreadState.ProcessingMessage;
        ResetTimer();

        try
        {
            await _actor!.HandleMessageAsync(message);
            _state = ActorThreadState.WaitingForMessage;
            ResetTimer();
        }
        catch (Exception ex)
        {
            _logger?.LogErrorEvent("ActorThread - {ActorMailboxId}", ex, "Actor message processing failed for mailbox {ActorMailboxId}.", _actor!.Id);
            SetFaulted(ex);
        }
    }

    /// <summary>
    /// Marks the actor thread as faulted and sets the associated exception.
    /// </summary>
    /// <remarks>This method transitions the actor thread to the faulted state and stops the
    /// associated channel.  If an error occurs while stopping the channel, it is suppressed.  The provided
    /// exception is stored for later retrieval or diagnostic purposes.</remarks>
    /// <param name="ex">The exception that caused the fault. This parameter cannot be <see langword="null"/>.</param>
    void SetFaulted(Exception ex)
    {
        _exception = ex;
        try { _threadScheduler.Stop(); }
        catch { }
        finally
        {
            _state = ActorThreadState.Faulted;
        }
    }

    /// <summary>
    /// Resets the timer by stopping the current timer (if any) and creating a new one with the specified
    /// timeout.
    /// </summary>
    /// <remarks>This method stops the current timer, if it exists, and disposes of its
    /// resources.  A new timer is then initialized with the configured timeout value and an infinite
    /// period.</remarks>
    void ResetTimer()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        _timer?.Dispose();
        _timer = new Timer(OnTimeout, null, _timeout, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Handles the timeout event for the actor's mailbox processing.
    /// </summary>
    /// <remarks>This method is invoked when the actor's mailbox processing exceeds the
    /// configured timeout duration. It stops the associated channel, updates the actor's state to indicate a
    /// timeout, and disposes of the timer.</remarks>
    /// <param name="state">An optional state object passed to the timeout callback. This parameter is not used in the method.</param>
    void OnTimeout(object? state)
    {
        try
        {
            var ex = new TimeoutException($"Actor mailbox processing timed out after {_timeout} for mailbox {_actor!.Id}");
            _exception = ex;
            try { Stop(); }
            catch { }
            finally { _state = ActorThreadState.TimedOut; }
            // Attempt to stop reading
            _reading = false;
            _timer?.Dispose();
        }
        catch { }
    }
}
