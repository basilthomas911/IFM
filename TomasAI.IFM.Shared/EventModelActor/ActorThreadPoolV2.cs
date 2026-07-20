using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides a thread pool implementation for managing and reusing actor threads, enabling efficient execution and
/// coordination of actor-based tasks.
/// </summary>
/// <remarks>ActorThreadPoolV2 implements the IActorThreadPool interface, allowing for initialization, retrieval,
/// and release of actor threads. It is designed to optimize resource usage in actor-based systems by pooling threads
/// and supporting dynamic allocation based on mailbox identifiers. Thread safety is ensured for concurrent
/// operations.</remarks>
/// <param name="supervisor">The supervisor responsible for managing the lifecycle and coordination of actors within the thread pool. Cannot be
/// null.</param>
/// <param name="logger">The logger used for recording events and errors related to the thread pool's operations. Cannot be null.</param>
public class ActorThreadPoolV2(IActorSupervisor supervisor, ILogger logger ) 
    : IActorThreadPool, IAsyncDisposable
{
    static readonly TimeSpan PoolExhaustionTimeout = TimeSpan.FromSeconds(30);
    readonly IActorSupervisor _supervisor = IsArgumentNull.Set(supervisor);
    readonly ConcurrentDictionary<ActorThreadId, IActorThread> _runningThreads = [];
    readonly BlockingThreadPoolQueue _threadPool = new(logger);
    readonly SemaphoreSlim[] _allocationLocks = CreateStripedLocks(16);
    readonly ILogger _logger = IsArgumentNull.Set(logger);

    static SemaphoreSlim[] CreateStripedLocks(int count)
    {
        var locks = new SemaphoreSlim[count];
        for (int i = 0; i < count; i++)
            locks[i] = new SemaphoreSlim(1, 1);
        return locks;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    SemaphoreSlim GetStripeLock(ActorThreadId threadId)
        => _allocationLocks[(threadId.GetHashCode() & 0x7FFF_FFFF) % _allocationLocks.Length];

    /// <summary>
    /// Initializes the thread pool with the specified number of threads.
    /// </summary>
    /// <remarks>Each thread's long-lived processing task is started immediately. The tasks park on an
    /// assignment signal until work is assigned via <see cref="GetThread"/>.</remarks>
    /// <param name="initialThreadCount">The number of threads to create and add to the thread pool. Must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="initialThreadCount"/> is less than or equal to zero.</exception>
    public IActorThreadPool Initialize(int initialThreadCount)
    {
        if (initialThreadCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialThreadCount), "Initial thread count must be greater than zero.");
        for (int i = 0; i < initialThreadCount; i++)
        {
            var thread = new ActorThreadV2(_supervisor, _logger);
            thread.Start();
            _threadPool.Enqueue(thread);
        }
        return this;
    }

    /// <summary>
    /// Retrieves the actor thread associated with the specified thread identifier.
    /// </summary>
    /// <remarks>Uses an inlined fast-path for the common case where the thread is already tracked.
    /// Falls back to <see cref="GetThreadSlow"/> when a new thread must be dequeued from the pool.</remarks>
    /// <param name="threadId">The identifier of the actor thread to retrieve. Cannot be null.</param>
    /// <returns>An instance of <see cref="IActorThread"/> representing the requested actor thread.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the actor associated with the mailbox ID of the specified thread identifier is not found in the
    /// context.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IActorThread GetThread(ActorThreadId threadId)
    {
        IsArgumentNull.Check(threadId);

        return _runningThreads.TryGetValue(threadId, out var actorThread)
            ? actorThread!
            : GetThreadSlow(threadId);
    }

    /// <summary>
    /// Slow path for <see cref="GetThread"/>: acquires a stripe lock, dequeues a thread from the pool,
    /// and tracks it. Kept out-of-line so the JIT can inline the fast path.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    IActorThread GetThreadSlow(ActorThreadId threadId)
    {
        var stripeLock = GetStripeLock(threadId);
        stripeLock.Wait();
        try
        {
            // Double-check after acquiring the lock to prevent two concurrent callers
            // from each dequeuing a thread for the same threadId (leaking one).
            if (_runningThreads.TryGetValue(threadId, out var actorThread))
                return actorThread!;

            var mailboxId = threadId.MailboxId;
            if (!_supervisor.Children.TryGetValue(mailboxId, out var actor))
                throw new KeyNotFoundException($"Actor with mailbox id '{mailboxId}' not found in context.");

            actorThread = _threadPool.Dequeue();
            actorThread.Start(actor, threadId);

            // Track the running thread so it can be retrieved/released later
            _runningThreads.TryAdd(threadId, actorThread);
            return actorThread!;
        }
        finally
        {
            stripeLock.Release();
        }
    }

    /// <summary>
    /// Releases the specified thread and returns it to the thread pool for future reuse.
    /// </summary>
    /// <remarks>If the specified thread is not currently running, this method has no effect. Releasing
    /// threads back to the pool helps optimize resource usage and improves performance in scenarios with frequent
    /// thread reuse.</remarks>
    /// <param name="threadId">The identifier of the thread to be released. Cannot be null.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReleaseThread(ActorThreadId threadId)
    {
        IsArgumentNull.Check(threadId);

        if (!_runningThreads.TryRemove(threadId, out var actorThread))
            return;
        _threadPool.Enqueue(actorThread);
    }

    /// <summary>
    /// Gets the number of threads currently in the thread pool.
    /// </summary>
    public int Count => _threadPool.Count;

    /// <summary>
    /// Asynchronously retrieves the actor thread associated with the specified thread identifier.
    /// </summary>
    /// <remarks>Uses a sync fast-path that returns a completed <see cref="ValueTask{T}"/> when the
    /// thread is already tracked in the running dictionary (steady-state common case). Falls back to
    /// <see cref="GetThreadAsyncSlow"/> only when a new thread must be dequeued from the pool.</remarks>
    /// <param name="threadId">The identifier of the actor thread to retrieve. Cannot be null.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>The requested actor thread, or throws if the pool is exhausted and the timeout elapses.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the actor associated with the mailbox ID is not found.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled or the pool exhaustion timeout elapses.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<IActorThread> GetThreadAsync(ActorThreadId threadId, CancellationToken ct)
    {
        IsArgumentNull.Check(threadId);

        return _runningThreads.TryGetValue(threadId, out var actorThread)
            ? new ValueTask<IActorThread>(actorThread!)
            : GetThreadAsyncSlow(threadId, ct);
    }

    /// <summary>
    /// Slow path for <see cref="GetThreadAsync"/>: acquires a stripe lock, dequeues a thread from the pool,
    /// and tracks it. Kept out-of-line so the JIT can inline the fast path.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    async ValueTask<IActorThread> GetThreadAsyncSlow(ActorThreadId threadId, CancellationToken ct)
    {
        var stripeLock = GetStripeLock(threadId);
        await stripeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring the lock to prevent two concurrent callers
            // from each dequeuing a thread for the same threadId (leaking one).
            if (_runningThreads.TryGetValue(threadId, out var actorThread))
                return actorThread!;

            var mailboxId = threadId.MailboxId;
            if (!_supervisor.Children.TryGetValue(mailboxId, out var actor))
                throw new KeyNotFoundException($"Actor with mailbox id '{mailboxId}' not found in context.");

            // Use a timeout to provide backpressure when the pool is exhausted
            actorThread = await _threadPool.DequeueAsync(PoolExhaustionTimeout, ct).ConfigureAwait(false);

            _runningThreads.TryAdd(threadId, actorThread);
            return actorThread!;
        }
        finally
        {
            stripeLock.Release();
        }
    }

    /// <summary>
    /// Asynchronously releases all resources used by the thread pool and stops any running threads.
    /// </summary>
    /// <remarks>Call this method to ensure that all threads managed by the pool are properly stopped and
    /// resources are released. Failing to call this method may result in resource leaks. After disposal, the object
    /// should not be used.</remarks>
    /// <returns>A ValueTask that represents the asynchronous dispose operation.</returns>
    public ValueTask DisposeAsync()
    {
        foreach (var thread in _runningThreads.Values)
            thread.Stop();

        while (_threadPool.Count > 0)
        {
            var thread = _threadPool.Dequeue();
            thread.Stop();
        }

        foreach (var stripeLock in _allocationLocks)
            stripeLock.Dispose();

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
