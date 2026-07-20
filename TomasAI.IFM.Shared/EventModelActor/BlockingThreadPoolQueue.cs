using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents a thread-safe queue for managing <see cref="IActorThread"/> instances in a thread pool.
/// </summary>
/// <remarks>This class provides mechanisms for enqueuing and dequeuing <see cref="IActorThread"/> instances, 
/// with support for both synchronous and asynchronous operations. It ensures proper synchronization  and blocking
/// behavior when the queue is empty, allowing threads to wait until new items are added.  The queue is designed to be
/// used in scenarios where thread management and coordination are required,  such as actor-based concurrency
/// models.</remarks>
/// <param name="logger"></param>
public sealed class BlockingThreadPoolQueue(ILogger logger)
{
    readonly ConcurrentQueue<IActorThread> _threadPoolQueue = new ();
    readonly SemaphoreSlim _waitSignal = new (0);
    readonly ILogger _logger = IsArgumentNull.Set(logger);

    /// <summary>
    /// Adds the specified thread to the thread pool queue.
    /// </summary>
    /// <remarks>This method is thread-safe. Each enqueued item increments the semaphore count,
    /// ensuring a 1:1 correspondence between enqueued items and available signals for consumers.</remarks>
    /// <param name="thread">The thread to enqueue. Cannot be <see langword="null"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(IActorThread thread)
    {
        _threadPoolQueue.Enqueue(thread);
        _waitSignal.Release();
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("EMA ThreadPool: Thread enqueued. Queue count: {Count}", _threadPoolQueue.Count);
    }

    /// <summary>
    /// Removes and returns the next available <see cref="IActorThread"/> from the queue.
    /// </summary>
    /// <remarks>If the queue is empty, the method blocks until an item is enqueued or the operation is
    /// canceled via the provided <paramref name="cancellationToken"/>.</remarks>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the wait operation if the queue is empty.</param>
    /// <returns>The next available <see cref="IActorThread"/> from the queue.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the queue is unexpectedly empty after the semaphore was signaled.</exception>
    public IActorThread Dequeue(CancellationToken cancellationToken = default)
    {
        _waitSignal.Wait(cancellationToken);

        if (!_threadPoolQueue.TryDequeue(out var thread))
            throw new InvalidOperationException("Queue was unexpectedly empty after semaphore was signaled.");

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("EMA ThreadPool: Thread dequeued. Queue count: {Count}", _threadPoolQueue.Count);
        return thread;
    }

    /// <summary>
    /// Asynchronously dequeues an <see cref="IActorThread"/> from the thread pool.
    /// </summary>
    /// <remarks>This method blocks asynchronously if the queue is empty, waiting for a thread to become
    /// available. The operation respects the provided <paramref name="cancellationToken"/> and will throw an exception
    /// if the token is canceled.</remarks>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    /// <returns>The next available <see cref="IActorThread"/> from the queue. If the queue is empty, the method waits until a
    /// thread is enqueued.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the queue is unexpectedly empty after the semaphore was signaled.</exception>
    public async ValueTask<IActorThread> DequeueAsync(CancellationToken cancellationToken = default)
    {
        await _waitSignal.WaitAsync(cancellationToken).ConfigureAwait(false);

        if (!_threadPoolQueue.TryDequeue(out var thread))
            throw new InvalidOperationException("Queue was unexpectedly empty after semaphore was signaled.");

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("EMA ThreadPool: Thread dequeued. Queue count: {Count}", _threadPoolQueue.Count);
        return thread;
    }

    /// <summary>
    /// Asynchronously dequeues an <see cref="IActorThread"/> from the thread pool with a timeout.
    /// </summary>
    /// <remarks>This overload avoids the per-call allocation of a linked <see cref="CancellationTokenSource"/>
    /// by using the <see cref="SemaphoreSlim.WaitAsync(TimeSpan, CancellationToken)"/> overload directly.</remarks>
    /// <param name="timeout">The maximum time to wait for a thread to become available.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    /// <returns>The next available <see cref="IActorThread"/> from the queue.</returns>
    /// <exception cref="OperationCanceledException">Thrown if the timeout elapses or the token is cancelled.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the queue is unexpectedly empty after the semaphore was signaled.</exception>
    public async ValueTask<IActorThread> DequeueAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!await _waitSignal.WaitAsync(timeout, cancellationToken).ConfigureAwait(false))
            throw new OperationCanceledException($"Thread pool exhausted: no thread available within {timeout}.");

        if (!_threadPoolQueue.TryDequeue(out var thread))
            throw new InvalidOperationException("Queue was unexpectedly empty after semaphore was signaled.");

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("EMA ThreadPool: Thread dequeued. Queue count: {Count}", _threadPoolQueue.Count);
        return thread;
    }

    /// <summary>
    /// Gets the number of elements currently contained in thread pool queue.
    /// </summary>
    public int Count { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _threadPoolQueue.Count; }
}
