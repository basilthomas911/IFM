using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents a thread responsible for processing messages for a single actor within an actor-based system. This class
/// manages the lifecycle and state of the actor thread, ensuring that messages are handled in a thread-safe manner.
/// </summary>
/// <remarks>This class is sealed and cannot be inherited. It coordinates message processing, thread state
/// transitions, and error handling for the actor thread. Thread safety is maintained throughout the message processing
/// lifecycle. The actor thread is managed by the provided supervisor and logs relevant events using the specified
/// logger.</remarks>
/// <param name="supervisor">The supervisor that manages the lifecycle and supervision of the actor associated with this thread. Cannot be null.</param>
/// <param name="logger">The logger used to record events and errors related to the actor thread. Cannot be null.</param>
sealed class ActorThreadV2(IActorSupervisor supervisor, ILogger logger) : IActorThread, IDisposable
{
    static readonly TimeSpan IdleDrainTimeout = TimeSpan.FromMilliseconds(50);
    readonly ILogger? _logger = IsArgumentNull.Set(logger);
    readonly IActorSupervisor _supervisor = IsArgumentNull.Set(supervisor);
    readonly SemaphoreSlim _messageAvailableSignal = new(0);
    readonly CancellationTokenSource _cts = new();
    volatile ActorThreadState _state = ActorThreadState.Ready;
    int _startOnce;
    Exception? _exception;
    Task? _processingTask;
    bool _disposed;

    public ActorThreadId Id { get; set; }

    /// <summary>
    /// Synchronously enqueues the message to the thread queue and signals the processing loop.
    /// </summary>
    /// <param name="message">The message to enqueue for the actor.</param>
    /// <returns><see langword="true"/> if the message was successfully written; otherwise, <see langword="false"/>.</returns>
    public bool Post(in NatsMsg<byte[]> message)
    {
        var msgSubject = message.Subject.ToSubject();
        Id = msgSubject.ThreadId;
        var actor = _supervisor.Children[Id.MailboxId];
        var threadQueue = actor.Mailbox.ThreadQueues.GetThreadQueue(Id);
        threadQueue.Start();
        var written = threadQueue.Write(message);
        if (written)
            SignalMessageAvailable(Id);
        return written;
    }

    /// <summary>
    /// Asynchronously enqueues the specified message to the thread queue associated with the target actor.
    /// </summary>
    /// <remarks>Uses a sync fast-path to avoid async state machine allocation when
    /// <see cref="IActorThreadQueue.EnqueueAsync"/> completes synchronously (common case with
    /// a non-full bounded channel).</remarks>
    /// <param name="message">The message to enqueue, containing the subject and payload data for the actor.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A value task that represents the asynchronous enqueue operation.</returns>
    public ValueTask WriteToActorThreadQueueAsync(NatsMsg<byte[]> message, CancellationToken cancellationToken = default)
    {
        var msgSubject = message.Subject.ToSubject();
        Id = msgSubject.ThreadId;
        var actor = _supervisor.Children[Id.MailboxId];
        var threadQueue = actor.Mailbox.ThreadQueues.GetThreadQueue(Id);
        var enqueueTask = threadQueue.EnqueueAsync(message, cancellationToken);
        if (enqueueTask.IsCompletedSuccessfully)
        {
            SignalMessageAvailable(Id);
            return ValueTask.CompletedTask;
        }
        return AwaitEnqueueAndSignalAsync(enqueueTask, Id);
    }

    /// <summary>
    /// Asynchronously enqueues the specified message using a pre-parsed subject to avoid redundant parsing.
    /// </summary>
    /// <remarks>Uses a sync fast-path to avoid async state machine allocation when
    /// <see cref="IActorThreadQueue.EnqueueAsync"/> completes synchronously.</remarks>
    /// <param name="message">The message to enqueue.</param>
    /// <param name="subject">The pre-parsed actor subject.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A value task representing the asynchronous operation.</returns>
    public ValueTask WriteToActorThreadQueueAsync(NatsMsg<byte[]> message, ActorSubject subject, CancellationToken cancellationToken = default)
    {
        Id = subject.ThreadId;
        var actor = _supervisor.Children[Id.MailboxId];
        var threadQueue = actor.Mailbox.ThreadQueues.GetThreadQueue(Id);
        var enqueueTask = threadQueue.EnqueueAsync(message, cancellationToken);
        if (enqueueTask.IsCompletedSuccessfully)
        {
            SignalMessageAvailable(Id);
            return ValueTask.CompletedTask;
        }
        return AwaitEnqueueAndSignalAsync(enqueueTask, Id);
    }

    /// <summary>
    /// Async fallback for <see cref="WriteToActorThreadQueueAsync"/> when the enqueue operation
    /// does not complete synchronously. Kept out-of-line so the JIT can inline the fast path.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    async ValueTask AwaitEnqueueAndSignalAsync(ValueTask enqueueTask, ActorThreadId threadId)
    {
        await enqueueTask.ConfigureAwait(false);
        SignalMessageAvailable(threadId);
    }

    /// <summary>
    /// Starts the actor thread's long-lived processing task.
    /// </summary>
    /// <remarks>This method creates a single background task that parks on the assignment signal between
    /// work assignments. It should be called once during pool initialization. Subsequent calls have no effect.
    /// Uses <see cref="Interlocked.CompareExchange"/> for thread-safe guard to prevent
    /// double task creation under concurrent calls.</remarks>
    /// <returns><see langword="true"/> if the thread was successfully started; otherwise, <see langword="false"/>.</returns>
    public bool Start()
    {
        if (Interlocked.CompareExchange(ref _startOnce, 1, 0) == 0)
        {
            _state = ActorThreadState.Started;
            _processingTask = Task.Run(ProcessAssignmentsAsync);
        }
        return true;
    }

    /// <summary>
    /// Signals that a message is available for processing by the specified actor thread.
    /// </summary>
    /// <remarks>Call this method to notify the actor thread represented by the specified thread ID that it
    /// can process a pending message. This is typically used in actor-based concurrency models to coordinate message
    /// handling between threads.</remarks>
    /// <param name="threadId">The identifier of the actor thread to be notified that a message is ready for processing.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SignalMessageAvailable(ActorThreadId threadId)
    {
        Id = threadId;
        _messageAvailableSignal.Release();
    }
    

    /// <summary>
    /// Starts the actor thread and assigns it the specified actor and thread identifier.
    /// </summary>
    /// <remarks>Sets the thread identity so that <see cref="ProcessAssignmentsAsync"/> can resolve
    /// the correct actor and thread queue when signalled. Does not signal; the caller is responsible
    /// for signalling after enqueuing a message.</remarks>
    /// <param name="actor">The actor to associate with this thread. Cannot be <see langword="null"/>.</param>
    /// <param name="threadId">The identifier of the thread to start. Cannot be <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the thread was successfully started; otherwise, <see langword="false"/>.</returns>
    public bool Start(IActor actor, ActorThreadId threadId)
    {
        Id = threadId;
        Start();
        return true;
    }

    /// <summary>
    /// Stops the actor thread and releases its resources.
    /// </summary>
    /// <remarks>This method cancels the long-lived processing task and releases the assignment signal
    /// to unblock any pending wait. If the thread is already stopped, the method has no effect.</remarks>
    /// <returns><see langword="true"/> to indicate that the stop operation was completed successfully.</returns>
    public bool Stop()
    {
        if (!IsStopped)
        {
            _state = ActorThreadState.Stopped;
            _cts.Cancel();
            _messageAvailableSignal.Release();
        }
        return true;
    }

    // Include state checks for runtime status.
    public bool IsRunning { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _state == ActorThreadState.ProcessingMessage || _state == ActorThreadState.WaitingForMessage; }
    public bool IsStarted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _state == ActorThreadState.Started || _state == ActorThreadState.ProcessingMessage || _state == ActorThreadState.WaitingForMessage; }
    public bool IsStopped { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _state == ActorThreadState.Stopped; }
    public bool IsFaulted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _state == ActorThreadState.Faulted; }
    public bool IsTimedOut { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _state == ActorThreadState.TimedOut; }
    public ActorThreadState State { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _state; }
    public Exception? Exception => _exception;

    
    /// <summary>
    /// Marks the actor thread as faulted and sets the associated exception.
    /// </summary>
    /// <param name="ex">The exception that caused the fault. This parameter cannot be <see langword="null"/>.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    void SetFaulted(Exception ex)
    {
        _state = ActorThreadState.Faulted;
        _exception = ex;
    }

    /// <summary>
    /// Releases the resources used by this actor thread, including the cancellation token source
    /// and assignment signal semaphore.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _cts.Dispose();
        _messageAvailableSignal.Dispose();
    }

    /// <summary>
    /// Long-lived processing loop that parks between work assignments.
    /// </summary>
    /// <remarks>The loop waits on the assignment signal, processes all messages from the assigned actor's
    /// thread queue, then releases the thread back to the pool and parks again. The task runs for the lifetime
    /// of the thread and is only terminated via cancellation (Stop).</remarks>
    async Task ProcessAssignmentsAsync()
    {
        var ct = _cts.Token;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _state = ActorThreadState.WaitingForMessage;
                    await _messageAvailableSignal.WaitAsync(ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested)
                        break;

                    if (Id == null)
                    {
                        if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
                            _logger.LogDebug("ActorThread: Received signal for message availability but thread ID is not set. Ignoring signal.");
                        continue;
                    }
                    var threadId = Id;
                    if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("ActorThread {ThreadId}: processing actor thread messages", threadId);
                    var actor = _supervisor.Children[threadId.MailboxId];
                    var threadQueue = actor.Mailbox.ThreadQueues.GetThreadQueue(threadId);

                    // Drain loop: process all available messages, then wait briefly for more
                    // before releasing the thread back to the pool. This avoids release/re-acquire
                    // churn under steady message load.
                    bool keepDraining = true;
                    while (keepDraining)
                    {
                        await foreach (var message in threadQueue.ReadAllAsync(ct).ConfigureAwait(false))
                        {
                            _state = ActorThreadState.ProcessingMessage;
                            await actor.HandleMessageAsync(message, threadId).ConfigureAwait(false);
                            _state = ActorThreadState.WaitingForMessage;
                        }

                        // Wait briefly for more messages before releasing the thread.
                        // If a new signal arrives within the window, drain again.
                        keepDraining = await _messageAvailableSignal.WaitAsync(IdleDrainTimeout, ct).ConfigureAwait(false);
                    }

                    // Release thread back to pool for reuse without stopping the task.
                    _supervisor.ThreadPool.ReleaseThread(threadId);

                    // Release thread queue to prevent unbounded memory growth.
                    actor.Mailbox.ThreadQueues.ReleaseThreadQueue(threadId);

                    // Drain accumulated semaphore signals to prevent spurious wake-ups
                    // that would re-enter the loop with a stale Id and block on an empty queue.
                    while (_messageAvailableSignal.Wait(0)) { }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_logger is not null && _logger.IsEnabled(LogLevel.Error))
                        _logger.LogErrorEvent(Id.ToString(), ex, "error processing message in actor thread queue.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogErrorEvent("ActorThread", ex, "ProcessAssignmentsAsync: error in assignment processing loop.");
            SetFaulted(ex);
        }
    }

}
